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

        private ThreadPoolTimer timer;
        private ThreadPoolTimer timerEndShow;

        private List<GpioPinValue> PinValues = new List<GpioPinValue>();
        private List<GpioPin> Pins = new List<GpioPin>();

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

            Pins.Add(GpioController.GetDefault().OpenPin(5));
            Pins[0].SetDriveMode(GpioPinDriveMode.Output);
            Pins[0].Write(GpioPinValue.High);

            Pins.Add(GpioController.GetDefault().OpenPin(6));
            Pins[1].SetDriveMode(GpioPinDriveMode.Output);
            Pins[1].Write(GpioPinValue.Low);

            Pins.Add(GpioController.GetDefault().OpenPin(23));
            Pins[2].SetDriveMode(GpioPinDriveMode.Output);
        }

        private async void ConnectionRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // GetDeferral is required for doing anything awaitable. GetDeferral informs the system that a background task may continue working 
            // after the IBackgroundTask.Run method returns. This is because the system may suspend or terminate the task after the Run method returns.
            var requestDeferral = args.GetDeferral();

            // A map with keys(String) and values(Object). Only serializable types are allowed.
            var returnMessage = new ValueSet();
            try
            {
                // Determine action from the client message
                var message = args.Request.Message["Request"] as string;
                switch (message)
                {
                    case "Turn LED On":
                        Pins[2].Write(GpioPinValue.High);
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        requestDeferral.Complete();                             // Lets the system know the asynchronous operation is complete
                        break;
                    case "Turn LED Off":
                        Pins[2].Write(GpioPinValue.Low);
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        requestDeferral.Complete();                             // Lets the system know the asynchronous operation is complete
                        break;
                    case "LED Show On":
                        LEDShow();
                        returnMessage.Add("Response", "OK");
                        await args.Request.SendResponseAsync(returnMessage);
                        break;
                    case "LED Show Off":
                        CompleteDeferral();
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
            timer = ThreadPoolTimer.CreatePeriodicTimer(TimerTick, TimeSpan.FromMilliseconds(500));

            // This timer will end the show
            timerEndShow = ThreadPoolTimer.CreatePeriodicTimer(CompleteDeferral, TimeSpan.FromMilliseconds(500));
        }

        private void TimerTick(ThreadPoolTimer timer)
        {
            SwitchLED(5);
            SwitchLED(6);
        }

        private void SwitchLED(int index)
        {
            Pins[index].Write((Pins[index].Read() == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High);
        }

        /// <summary>
        /// When done with timer, it should be cancelled.
        /// </summary>
        /// <param name="timer"></param>
        private void CancelTimer(ThreadPoolTimer timer)
        {
            if (timer != null)
            {
                timer.Cancel();
            }
        }

        /// <summary>
        /// Signature for CreatePeriodicTimer
        /// </summary>
        /// <param name="timer"></param>
        private void CompleteDeferral(ThreadPoolTimer timer)
        {
            // Cancels or ends a timer
            CancelTimer(timer);
            CompleteDeferral();
        }

        /// <summary>
        /// Signature needed for OnTaskCancelled delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="reason"></param>
        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            CompleteDeferral();
        }

        /// <summary>
        /// Completes a deferral if it exists
        /// </summary>
        private void CompleteDeferral()
        {
            if (_deferral != null)
            {
                // Lets the system know the asynchronous operation is complete
                _deferral.Complete();
            }
        }
    }
}
