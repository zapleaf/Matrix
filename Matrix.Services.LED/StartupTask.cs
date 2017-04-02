using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;

using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using Windows.Devices.Gpio;
using Windows.Foundation.Collections;
using Windows.System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace Matrix.Services.LED
{
    public sealed class StartupTask : IBackgroundTask
    {
        /// <summary>
        /// BackgroundTaskDeferral prevents the task from closing prematurely (http://aka.ms/backgroundtaskdeferral)
        /// </summary>
        private BackgroundTaskDeferral _deferral;

        /// <summary>
        /// Endpoint connection for an app service. App services enable app-to-app communication by allowing you to provide 
        /// services from your Universal Windows app to other Universal Windows app.
        /// </summary>
        private AppServiceConnection _connection;


        private ThreadPoolTimer timer5;

        private int _ledPinNumber = 23;
        private GpioPin _ledPin;

        private int _ledShowPinNumber = 5;
        private GpioPin _ledShowPin;
        private GpioPinValue pinValue = new GpioPinValue();

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Keep BackgroundTask alive
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnTaskCanceled;

            // Execution triggered by another application requesting this App Service
            // assigns an event handler to fire when a message is received from the client
            var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            _connection = triggerDetails.AppServiceConnection;
            _connection.RequestReceived += ConnectionRequestReceived;

            // Initialize the LED pin
            GpioController controller = GpioController.GetDefault();
            _ledPin = controller.OpenPin(_ledPinNumber);
            _ledPin.SetDriveMode(GpioPinDriveMode.Output);

            _ledShowPin = controller.OpenPin(_ledShowPinNumber);
            _ledShowPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private async void ConnectionRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // if you are doing anything awaitable, you need to get a deferral
            var requestDeferral = args.GetDeferral();
            var returnMessage = new ValueSet();
            try
            {
                //obtain and react to the command passed in by the client
                var message = args.Request.Message["Request"] as string;
                switch (message)
                {
                    case "Turn LED On":
                        _ledPin.Write(GpioPinValue.High);
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        //let the OS know that the action is complete
                        requestDeferral.Complete();
                        break;
                    case "Turn LED Off":
                        _ledPin.Write(GpioPinValue.Low);
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        //let the OS know that the action is complete
                        requestDeferral.Complete();
                        break;
                    case "LED Show":
                        LEDShow();
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        break;
                }    
            }
            catch (Exception ex)
            {
                returnMessage.Add("Response", "Failed: " + ex.Message);
            }
        }

        private void LEDShow()
        {
            // Creates a periodic timer and specifies a method to call after the periodic timer is complete. 
            // The periodic timer is complete when the timer has expired without being reactivated, and the final call to handler has finished.
            timer5 = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick5, TimeSpan.FromMilliseconds(500));
        }

        private void Timer_Tick5(ThreadPoolTimer timer) { SwitchLED(0); }

        private void SwitchLED(int index)
        {
            pinValue = (pinValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;
            _ledShowPin.Write(pinValue);
        }


        /// <summary>
        /// When done with timer, it should be cancelled.
        /// </summary>
        /// <param name="timer"></param>
        private void CloseTimer(ThreadPoolTimer timer)
        {
            if (timer != null)
            {
                timer.Cancel();
            }
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (_deferral != null)
            {
                _deferral.Complete();
            }
        }
    }
}
