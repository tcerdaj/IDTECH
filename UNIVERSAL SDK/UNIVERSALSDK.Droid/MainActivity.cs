using System;
using System.Collections.Generic;
using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Idtechproducts.Device;
using Com.Idtechproducts.Device.Bluetooth;
using static Android.Bluetooth.BluetoothAdapter;
using static Com.Idtechproducts.Device.ReaderInfo;
using System.Linq;
using Android.Support.V4.Content;
using Android;
using Android.Support.V4.App;
using System.Linq;
using System.Collections;

namespace UNIVERSALSDK.Droid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, IOnReceiverListener
    {
        private IDT_VP3300 myVP3300Reader = null;
        private BluetoothAdapter mBtAdapter = null;
        private long BLE_ScanTimeout = 30000; //in milliseconds
        private BLEScanCallback mLeScanCallback;
        BluetoothLeScanner bleScanner;
        View view;
        TextView welcomeText;
        static Android.OS.Handler hanlder;
        bool isScanning, transactionCompleted;
        List<ScanFilter> filters;
        private static long START_GATT_DELAY = 500; // msec
        private Handler mStartGattHandler = new Handler();
        static BluetoothDevice _device;
       

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            welcomeText = (TextView)FindViewById(Resource.Id.welcomeText);
            welcomeText.Text = "Swipe payment";
            Log.Info("Initialize", "Init...");
            hanlder = new Handler();
            isScanning = false;

            filters = filters ?? new List<ScanFilter>();

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;
        }

        private void scanLeDevice(bool enable, long timeout)
        {
            hanlder.Post(() =>
            {

                var permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation);
                int resquestCode = 2;

                if (permissionCheck != Android.Content.PM.Permission.Granted)
                {
                    welcomeText.Text = "Access Fine Location Permission required for scanning";
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessFineLocation.ToString() }, resquestCode);
                    return;
                }

                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation);
                if (permissionCheck != Android.Content.PM.Permission.Granted)
                {
                    welcomeText.Text = "Access Coarse Location Permission required for scanning";
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessCoarseLocation.ToString() }, resquestCode);
                    return;
                }

                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth);
                if (permissionCheck != Android.Content.PM.Permission.Granted)
                {
                    welcomeText.Text = "Bluetooth Permission required for scanning";
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.Bluetooth.ToString() }, resquestCode);
                    return;
                }

                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdmin);
                if (permissionCheck != Android.Content.PM.Permission.Granted)
                {
                    welcomeText.Text = "Bluetooth Admin Permission required for scanning";
                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.BluetoothAdmin.ToString() }, resquestCode);
                    return;
                }

                var handler = new Android.OS.Handler();
                welcomeText.Text = "Scanning...";
                Log.Info("scanLeDevice", "Scanning...");
                if (enable)
                {
                    if (isScanning)
                        bleScanner.StopScan(mLeScanCallback);

                    bleScanner.UnregisterFromRuntime();
                    bleScanner.FlushPendingScanResults(mLeScanCallback);
                    ScanSettings settings = new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency).Build();

                    handler.PostDelayed(() =>
                    {
                        if (!myVP3300Reader.Device_isConnected() && !transactionCompleted)
                        {
                            welcomeText.Text = "Timeout";
                            Snackbar.Make(view, "Timeout", Snackbar.LengthLong)
                        .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
                            if (isScanning)
                                bleScanner.StopScan(mLeScanCallback);
                        }
                    }, timeout);

                    isScanning = true;
                    bleScanner.StartScan(filters, settings, mLeScanCallback);
                }
                else
                {
                    if (isScanning)
                        bleScanner.StopScan(mLeScanCallback);
                }
            });
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            view = (View)sender;
            Snackbar.Make(view, "Scanning...", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();

            welcomeText.Text = "";
            transactionCompleted = false;

            if (myVP3300Reader != null)
            {
                myVP3300Reader.UnregisterListen();
                myVP3300Reader.Release();
                // myVP3300Reader = null;
            }

            myVP3300Reader = new IDT_VP3300(this, Application.Context);
            myVP3300Reader.RegisterListen();

            if (myVP3300Reader.Device_setDeviceType(DEVICE_TYPE.DeviceVp3300Bt))
            {

                mBtAdapter = BluetoothAdapter.DefaultAdapter;
                var REQUEST_ENABLE_BT = 1;

                if (!mBtAdapter.IsEnabled)
                {
                    var enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                    StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
                }

                bleScanner = mBtAdapter.BluetoothLeScanner;
                mLeScanCallback = new BLEScanCallback() { RegisterBLE = RegisterBLE };
                scanLeDevice(true, BLE_ScanTimeout);
            }
        }

        public void RegisterBLE(IList<ParcelUuid> uuids)
        {
            Log.Info("RegisterBLE", "RegisterBLE");
            isScanning = false;
            bleScanner.StopScan(mLeScanCallback);
            SetFilters(uuids);
            myVP3300Reader.RegisterListen();
        }

        private void SetFilters(IList<ParcelUuid> uuids)
        {
            if (uuids != null)
            {
                foreach (var uuid in uuids)
                {
                    ScanFilter sf = new ScanFilter.Builder()
                        .SetServiceUuid(uuid)
                        .SetDeviceAddress(_device.Address)
                        .SetDeviceName(_device.Name)
                        .Build();
                        
                    filters.Add(sf);
                }
            }
        }

        public void ICCNotifyInfo(byte[] p0, string p1)
        {
            throw new NotImplementedException();
        }

        public void LoadXMLConfigFailureInfo(int p0, string p1)
        {
            throw new NotImplementedException();
        }

        public void AutoConfigCompleted(StructConfigParameters p0)
        {
            throw new NotImplementedException();
        }

        public void AutoConfigProgress(int p0)
        {
            throw new NotImplementedException();
        }

        public void DataInOutMonitor(byte[] p0, bool p1)
        {
            //welcomeText.Text = "DataInOutMonitor";
            Log.Info("DataInOutMonitor", "DataInOutMonitor");
        }

        public void DeviceConnected()
        {
            hanlder.Post(async () =>
            {
                welcomeText.Text = "Device Connected";
                Log.Info("DeviceConnected", "Connected");
                var data = new ResDataStruct();
                var result = myVP3300Reader.Device_sendDataCommand("F002", false, null, data);
                if (data.ResData != null)
                {
                    var batteryLevel = data.ResData[0];
                    string level = "Medium";
                    if (batteryLevel >= 210)
                        level = "Full";
                    else if (batteryLevel <= 192)
                        level = "Low";

                    welcomeText.Text = "Battery level " + level;
                    await System.Threading.Tasks.Task.Delay(5000);
                }

                var errorCode = myVP3300Reader.Device_startTransaction(1, 0, 0, 30, null);
                var message = myVP3300Reader.Device_getResponseCodeString(errorCode);
                welcomeText.Text += "Transaction Results: " + message;
            });
        }

        public void DeviceDisconnected()
        {
            hanlder.Post(() =>
            {
                Log.Info("DeviceDisconnected", "Disconnected");

                if (!isScanning)
                    welcomeText.Text = "Device Disconnected";
            });
        }
        string detail = "";

        public void EmvTransactionData(IDTEMVData emvData)
        {
            hanlder.Post(async () =>
            {

                detail += Common.EmvErrorCodes(emvData.Result);
                detail += "nrnn";

                if (emvData.Result == IDTEMVData.StartTransSuccess)
                    detail += "Start transaction response:nrnn";
                else if (emvData.Result == IDTEMVData.GoOnline)
                    detail += "nrnnAuthentication response:nrnn";
                else
                    detail += "nrnnComplete Transaction response:nrnn";
                if (emvData.UnencryptedTags != null && emvData.UnencryptedTags.Count > 0)
                {
                    detail += "Unencrypted Tags:nrnn";
                    foreach (DictionaryEntry key in emvData.UnencryptedTags)
                    {

                        detail += key.Key + ": ";

                        byte[] data = (byte[])key.Value;
                        detail += Common.GetHexStringFromBytes(data) + "nrnn";
                    }
                }
                if (emvData.MaskedTags != null && emvData.MaskedTags.Count > 0)
                {
                    detail += "Masked Tags:nrnn";
                    var keys = emvData.MaskedTags.Keys;
                    foreach (var key in keys)
                    {
                        detail += key + ": ";
                        byte[] data = (byte[])emvData.MaskedTags[(object)key];
                        detail += Common.GetHexStringFromBytes(data) + "nrnn";
                    }
                }
                if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count > 0)
                {
                    detail += "Encrypted Tags:nrnn";
                    var keys = emvData.EncryptedTags.Keys;
                    foreach (var key in keys)
                    {
                        detail += key + ": ";
                        byte[] data = (byte[])emvData.EncryptedTags[(object)key];
                        detail += Common.GetHexStringFromBytes(data) + "nrnn";
                    }
                }

                welcomeText.Text = detail;
                Log.Info("EmvTransactionData", detail);

                if (emvData.Result == IDTEMVData.GoOnline)
                {
                    //Auto Complete
                    byte[] response = new byte[] { 0x30, 0x30 };
                    myVP3300Reader.Emv_completeTransaction(false, response, null, null, null);
                }
                else if (emvData.Result == IDTEMVData.StartTransSuccess)
                {
                    //Auto Authenticate
                    myVP3300Reader.Emv_authenticateTransaction(null);
                }
                else
                {
                    await System.Threading.Tasks.Task.Delay(2000);
                    Log.Info("EmvTransactionData", "Transaction completed");
                    transactionCompleted = true;
                    ReleaseDevice();
                }
            });
        }

     
        public void LcdDisplay(int mode, string[] lines, int timeout)
        {
            hanlder.Post(async () =>
            {
                Log.Info("LcdDisplay", lines[0]);
                if (mode == 0x01) //Menu Display
                {
                    //automatically select 1st application
                    myVP3300Reader.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
                }
                else if (mode == 0x08) //Language Menu Display
                {
                    //automatically select first language
                    myVP3300Reader.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
                }
                else if (lines[0].ToLower().Contains("timeout"))
                {
                    ResDataStruct toData = new ResDataStruct();
                    welcomeText.Text = "Feedback: " + lines[0];
                    await System.Threading.Tasks.Task.Delay(2000);
                    ReleaseDevice();
                }
                else
                {
                    welcomeText.Text = "Feedback: " + lines[0];
                }
            });
        }

        public void TransactionFeedBack(int p0, string[] p1, int p2, byte[] p3, sbyte p4)
        {
            hanlder.Post(() =>
            {
                Log.Info("TransactionFeedBack", p1[0]);
                welcomeText.Text = "TransactionFeedBack: " + p1[0];
            });
        }

        public void MsgAudioVolumeAjustFailed()
        {
            throw new NotImplementedException();
        }

        public void MsgBatteryLow()
        {
            throw new NotImplementedException();
        }

        public void MsgRKICompleted(string p0)
        {
            throw new NotImplementedException();
        }

        public void MsgToConnectDevice()
        {
            throw new NotImplementedException();
        }

        public void SwipeMSRData(IDTMSRData p0)
        {
            throw new NotImplementedException();
        }

        public void Timeout(int erroCode)
        {
            welcomeText.Text = "Timeout - message: " + ErrorCodeInfo.GetErrorCodeDescription(erroCode);
            Log.Info("Timeout", "Message..." + ErrorCodeInfo.GetErrorCodeDescription(erroCode));
            hanlder.PostDelayed(() => {
                ReleaseDevice();
            }, 5000);

        }

        void ReleaseDevice()
        {
            Log.Info("ReleaseDevice", "Releasing...");
            if (myVP3300Reader != null)
            {
                myVP3300Reader.UnregisterListen();
                myVP3300Reader.Release();
                //myVP3300Reader = null;
            }
        }

        public class BLEScanCallback : Android.Bluetooth.LE.ScanCallback
        {
            const string OUI = "00:1C:97"; // IDtech's MAC Address Organizationally Unique Identifier
            public Action<IList<ParcelUuid>> RegisterBLE { get; set; }
            public bool _registered = false;

            public override void OnBatchScanResults(IList<ScanResult> results)
            {
                // not used 
            }

            public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult r)
            {
                //System.Diagnostics.Debug.WriteLine("*** Bluetooth LE Device found - Name: {0}, MAC: {1} ", result.ScanRecord.DeviceName ?? result.Device.Name ?? "No Name", result.Device.Address);

                //if (!string.IsNullOrEmpty(result.Device.Name) && result.Device.Name.ToUpper().Contains("IDTECH"))
                //if (result.Device.Address == "00:1C:97:18:77:34")
                hanlder.Post(() =>
                {
                    Log.Info("OnScanResult", "Device..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Address));
                    Log.Info("OnScanResult", "Device Name..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Name));
                    System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
                    System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);
                 

                    if (r.Device.Address.StartsWith(OUI))
                    {
                        if (!_registered)
                        {
                            System.Diagnostics.Debug.WriteLine("**************FOUND*************");
                            System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
                            System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);
                            System.Diagnostics.Debug.WriteLine("    tXPower " + r.ScanRecord.TxPowerLevel);
                            System.Diagnostics.Debug.WriteLine("    Rssi " + r.Rssi);

                            _device = r.Device;
                            var uuids = r.ScanRecord.ServiceUuids;
                            Log.Info("OnScanResult", "Device found " + r.Device.Name);
                            Com.Idtechproducts.Device.Common.BLEDeviceName = r.Device.Name;
                            BluetoothLEController.SetBluetoothDevice(r.Device);
                            _registered = true;
                            RegisterBLE?.Invoke(uuids);
                        }
                    }
                });
            }

            public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
            {
                var message = "Bluetooth scan failed.";

                Log.Info("OnScanFailed", "Error..." + errorCode.ToString());
                // Feedback?.Invoke(new IDTechEvents { IDTechEvent = IDTechEvents.eIDTechEvents.CardReaderFailure, Value = message });

                try
                {
                    if (errorCode == ScanFailure.ApplicationRegistrationFailed)
                    {
                        BluetoothAdapter.DefaultAdapter.Disable();
                        BluetoothAdapter.DefaultAdapter.Enable();
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }



   


}

