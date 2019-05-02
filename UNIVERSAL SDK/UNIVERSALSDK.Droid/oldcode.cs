//using System;
//using System.Collections.Generic;
//using Android.App;
//using Android.Bluetooth;
//using Android.Bluetooth.LE;
//using Android.Content;
//using Android.OS;
//using Android.Runtime;
//using Android.Support.Design.Widget;
//using Android.Support.V7.App;
//using Android.Util;
//using Android.Views;
//using Android.Widget;
//using Com.Idtechproducts.Device;
//using Com.Idtechproducts.Device.Bluetooth;
//using static Android.Bluetooth.BluetoothAdapter;
//using static Com.Idtechproducts.Device.ReaderInfo;
//using Android.Support.V4.Content;
//using Android;
//using Android.Support.V4.App;
//using System.Collections;
//using System.Runtime.Serialization.Formatters.Binary;
//using System.IO;
//using System.Text;
//using System.Threading;

//namespace UNIVERSALSDK.Droid
//{
//    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
//    public class MainActivity : AppCompatActivity, IOnReceiverListener
//    {
//        private IDT_VP3300 myVP3300Reader = null;
//        private BluetoothAdapter mBtAdapter = null;
//        private long BLE_ScanTimeout = 30000; //in milliseconds
//        static BLEScanCallback mLeScanCallback;
//        static BluetoothLeScanner bleScanner;
//        View view;
//        TextView welcomeText;
//        ImageView batteryLifeView;
//        static Android.OS.Handler hanlder;
//        bool isScanning, transactionCompleted;
//        List<ScanFilter> filters;
//        private Handler mStartGattHandler = new Handler();
//        static BluetoothDevice _device;
//        const string DEVICE_FILTERS = "DeviceFilters", DEVICE_ADDRESS = "DeviceAddress";
//        static ISharedPreferences sharedPre;
//        static BluetoothGatt gatt;
//        BluetoothManager _manager;

//        protected override void OnCreate(Bundle savedInstanceState)
//        {
//            base.OnCreate(savedInstanceState);
//            SetContentView(Resource.Layout.activity_main);

//            batteryLifeView = (ImageView)FindViewById(Resource.Id.batteryLife);
//            welcomeText = (TextView)FindViewById(Resource.Id.welcomeText);
//            welcomeText.Text = "Swipe payment";
//            Log.Info("Initialize", "Init...");
//            hanlder = new Handler();
//            isScanning = false;

//            sharedPre = Application.Context.GetSharedPreferences(DEVICE_FILTERS, FileCreationMode.Private);

//            filters = filters ?? new List<ScanFilter>();

//            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
//            SetSupportActionBar(toolbar);

//            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
//            fab.Click += FabOnClick;
//        }

//        private void scanLeDevice(bool enable, long timeout)
//        {
//            hanlder.Post(() =>
//            {

//                var permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation);
//                int resquestCode = 2;

//                if (permissionCheck != Android.Content.PM.Permission.Granted)
//                {
//                    welcomeText.Text = "Access Fine Location Permission required for scanning";
//                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessFineLocation.ToString() }, resquestCode);
//                    return;
//                }

//                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation);
//                if (permissionCheck != Android.Content.PM.Permission.Granted)
//                {
//                    welcomeText.Text = "Access Coarse Location Permission required for scanning";
//                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessCoarseLocation.ToString() }, resquestCode);
//                    return;
//                }

//                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth);
//                if (permissionCheck != Android.Content.PM.Permission.Granted)
//                {
//                    welcomeText.Text = "Bluetooth Permission required for scanning";
//                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.Bluetooth.ToString() }, resquestCode);
//                    return;
//                }

//                permissionCheck = ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdmin);
//                if (permissionCheck != Android.Content.PM.Permission.Granted)
//                {
//                    welcomeText.Text = "Bluetooth Admin Permission required for scanning";
//                    ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.BluetoothAdmin.ToString() }, resquestCode);
//                    return;
//                }

//                var handler = new Android.OS.Handler();
//                welcomeText.Text = "Scanning...";
//                Log.Info("scanLeDevice", "Scanning...");
//                batteryLifeView.Visibility = ViewStates.Invisible;

//                if (enable)
//                {
//                    var scanFilter = new ScanFilter.Builder().Build();
//                    bleScanner.FlushPendingScanResults(mLeScanCallback);
//                    ScanSettings settings = new ScanSettings
//                                                 .Builder()
//                                                 .SetCallbackType(ScanCallbackType.AllMatches)
//                                                 .SetMatchMode(BluetoothScanMatchMode.Aggressive)
//                                                 .SetScanMode(Android.Bluetooth.LE.ScanMode.LowPower)
//                                                 .SetReportDelay(timeout)
//                                                 .Build();


//                    handler.PostDelayed(() =>
//                    {
//                        if (myVP3300Reader != null && !myVP3300Reader.Device_isConnected() && !transactionCompleted)
//                        {
//                            welcomeText.Text = "Timeout";
//                            Snackbar.Make(view, "Timeout", Snackbar.LengthLong)
//                        .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
//                            if (isScanning)
//                                bleScanner.StopScan(mLeScanCallback);
//                        }
//                    }, timeout);

//                    isScanning = true;
//                    var success = _manager.Adapter.StartDiscovery();
//                    bleScanner.StartScan(new List<ScanFilter> { scanFilter }, settings, mLeScanCallback);
//                }
//                else
//                {
//                    if (isScanning)
//                        bleScanner.StopScan(mLeScanCallback);
//                }
//            });
//        }

//        public override bool OnCreateOptionsMenu(IMenu menu)
//        {
//            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
//            return true;
//        }

//        public override bool OnOptionsItemSelected(IMenuItem item)
//        {
//            int id = item.ItemId;
//            if (id == Resource.Id.action_settings)
//            {
//                return true;
//            }

//            return base.OnOptionsItemSelected(item);
//        }

//        private void FabOnClick(object sender, EventArgs eventArgs)
//        {
//            view = (View)sender;
//            Snackbar.Make(view, "Scanning...", Snackbar.LengthLong)
//                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();

//            welcomeText.Text = "";
//            transactionCompleted = false;

//            _manager = (BluetoothManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService);

//            mBtAdapter = BluetoothAdapter.DefaultAdapter;

//            bleScanner = mBtAdapter.BluetoothLeScanner;
//            //ReleaseDevice();

//            myVP3300Reader = new IDT_VP3300(this, Application.Context);
//            myVP3300Reader.UnregisterListen();

//            if (myVP3300Reader.Device_setDeviceType(DEVICE_TYPE.DeviceVp3300Bt))
//            {
//                var REQUEST_ENABLE_BT = 1;

//                if (!mBtAdapter.IsEnabled)
//                {
//                    var enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
//                    StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
//                }

//                mLeScanCallback = new BLEScanCallback() { RegisterBLE = RegisterBLE };
//                scanLeDevice(true, BLE_ScanTimeout);
//            }
//        }

//        public void RegisterBLE(IList<ParcelUuid> uuids)
//        {
//            Log.Info("RegisterBLE", "RegisterBLE");
//            isScanning = false;
//            myVP3300Reader.RegisterListen();
//        }

//        private void SetFilters(IList<ParcelUuid> uuids)
//        {
//            if (uuids != null)
//            {
//                foreach (var uuid in uuids)
//                {
//                    ScanFilter sf = new ScanFilter.Builder()
//                        .SetServiceUuid(uuid)
//                        .SetDeviceAddress(_device.Address)
//                        .SetDeviceName(_device.Name)
//                        .Build();

//                    filters.Add(sf);
//                }
//            }
//        }

//        private IList<ScanFilter> GetFilters()
//        {
//            IList<ScanFilter> result = new List<ScanFilter>();

//            try
//            {
//                string adddressStr = sharedPre.GetString(DEVICE_ADDRESS, "");

//                string[] addresses = adddressStr.Split("|");

//                foreach (var add in addresses)
//                {
//                    ScanFilter sf = new ScanFilter.Builder()
//                        .SetDeviceAddress(_device.Address)
//                        .Build();

//                    result.Add(sf);
//                }
//            }
//            catch (Exception ex)
//            {

//                Log.Error("GetFilters", ex.Message);
//            }

//            return result;
//        }

//        public void ICCNotifyInfo(byte[] p0, string p1)
//        {
//            throw new NotImplementedException();
//        }

//        public void LoadXMLConfigFailureInfo(int p0, string p1)
//        {
//            throw new NotImplementedException();
//        }

//        public void AutoConfigCompleted(StructConfigParameters p0)
//        {
//            throw new NotImplementedException();
//        }

//        public void AutoConfigProgress(int p0)
//        {
//            throw new NotImplementedException();
//        }

//        public void DataInOutMonitor(byte[] p0, bool p1)
//        {
//            //welcomeText.Text = "DataInOutMonitor";
//            Log.Info("DataInOutMonitor", "DataInOutMonitor");
//        }

//        public void DeviceConnected()
//        {
//            hanlder.Post(async () =>
//            {
//                welcomeText.Text = "Device Connected";
//                Log.Info("DeviceConnected", "Connected");
//                var data = new ResDataStruct();
//                var result = myVP3300Reader.Device_sendDataCommand("F002", false, null, data);
//                if (data.ResData != null)
//                {
//                    var batteryLevel = data.ResData[0];
//                    string level = "Medium";
//                    if (batteryLevel >= 210)
//                        level = "Full";
//                    else if (batteryLevel <= 192)
//                        level = "Low";

//                    //welcomeText.Text = "Battery level " + level;
//                    batteryLifeView.Visibility = ViewStates.Visible;
//                    int image = level == "Full" ? Resource.Mipmap.full : level == "Low" ? Resource.Mipmap.low : Resource.Mipmap.medium;
//                    batteryLifeView.SetImageResource(image);
//                    await System.Threading.Tasks.Task.Delay(5000);
//                }

//                var errorCode = myVP3300Reader.Device_startTransaction(1, 0, 0, 30, null);
//                var message = myVP3300Reader.Device_getResponseCodeString(errorCode);
//                welcomeText.Text += "Transaction Results: " + message;
//            });
//        }

//        public void DeviceDisconnected()
//        {
//            hanlder.Post(() =>
//            {
//                Log.Info("DeviceDisconnected", "Disconnected");

//                if (!isScanning)
//                {
//                    welcomeText.Text = "Device Disconnected";
//                    batteryLifeView.Visibility = ViewStates.Invisible;
//                    ReleaseDevice();

//                    if (gatt != null && _manager != null && _manager.GetConnectionState(gatt.Device, ProfileType.Gatt) == ProfileState.Connected)
//                    {
//                        gatt?.Disconnect();
//                        myVP3300Reader = null;
//                    }
//                }
//            });
//        }
//        string detail = "";

//        public void EmvTransactionData(IDTEMVData emvData)
//        {
//            hanlder.Post(async () =>
//            {
//                await EmvDataHanldler(emvData);
//            });
//        }

//        private async System.Threading.Tasks.Task EmvDataHanldler(object dataEntry)
//        {
//            if (dataEntry.GetType() == typeof(IDTEMVData))
//            {
//                var emvData = dataEntry as IDTEMVData;
//                if (emvData != null)
//                {

//                    detail += Common.EmvErrorCodes(emvData.Result);
//                    detail += "nrnn";

//                    if (emvData.Result == IDTEMVData.StartTransSuccess)
//                        detail += "Start transaction response:nrnn";
//                    else if (emvData.Result == IDTEMVData.GoOnline)
//                        detail += "nrnnAuthentication response:nrnn";
//                    else
//                        detail += "nrnnComplete Transaction response:nrnn";
//                    if (emvData.UnencryptedTags != null && emvData.UnencryptedTags.Count > 0)
//                    {
//                        detail += "Unencrypted Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.UnencryptedTags)
//                        {

//                            detail += key.Key + ": ";
//                            var d = emvData.UnencryptedTags[key.Key];

//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }
//                    if (emvData.MaskedTags != null && emvData.MaskedTags.Count > 0)
//                    {
//                        detail += "Masked Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.MaskedTags)
//                        {
//                            detail += key + ": ";
//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }
//                    if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count > 0)
//                    {
//                        detail += "Encrypted Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.EncryptedTags)
//                        {
//                            detail += key + ": ";
//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }

//                    welcomeText.Text = detail;
//                    Log.Info("EmvTransactionData", detail);

//                    if (emvData.Result == IDTEMVData.GoOnline)
//                    {
//                        //Auto Complete
//                        byte[] response = new byte[] { 0x30, 0x30 };
//                        myVP3300Reader.Emv_completeTransaction(false, response, null, null, null);
//                    }
//                    else if (emvData.Result == IDTEMVData.StartTransSuccess)
//                    {
//                        //Auto Authenticate
//                        myVP3300Reader.Emv_authenticateTransaction(null);
//                    }
//                    else
//                    {
//                        await System.Threading.Tasks.Task.Delay(2000);
//                        Log.Info("EmvTransactionData", "Transaction completed");
//                        transactionCompleted = true;
//                        ReleaseDevice();
//                    }
//                }
//            }
//            else if (dataEntry.GetType() == typeof(IDTMSRData))
//            {
//                var emvData = dataEntry as IDTMSRData;

//                if (emvData != null)
//                {
//                    detail += Common.EmvErrorCodes(emvData.Result);
//                    detail += "nrnn";

//                    if (emvData.Result == IDTEMVData.StartTransSuccess)
//                        detail += "Start transaction response:nrnn";
//                    else if (emvData.Result == IDTEMVData.GoOnline)
//                        detail += "nrnnAuthentication response:nrnn";
//                    else
//                        detail += "nrnnComplete Transaction response:nrnn";
//                    if (emvData.UnencryptedTags != null && emvData.UnencryptedTags.Count > 0)
//                    {
//                        detail += "Unencrypted Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.UnencryptedTags)
//                        {

//                            detail += key.Key + ": ";
//                            var d = emvData.UnencryptedTags[key.Key];

//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }
//                    if (emvData.MaskedTags != null && emvData.MaskedTags.Count > 0)
//                    {
//                        detail += "Masked Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.MaskedTags)
//                        {
//                            detail += key + ": ";
//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }
//                    if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count > 0)
//                    {
//                        detail += "Encrypted Tags:nrnn";
//                        foreach (DictionaryEntry key in emvData.EncryptedTags)
//                        {
//                            detail += key + ": ";
//                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
//                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
//                        }
//                    }

//                    welcomeText.Text = detail;
//                    Log.Info("EmvTransactionData", detail);

//                    if (emvData.Result == IDTEMVData.GoOnline)
//                    {
//                        //Auto Complete
//                        byte[] response = new byte[] { 0x30, 0x30 };
//                        myVP3300Reader.Emv_completeTransaction(false, response, null, null, null);
//                    }
//                    else if (emvData.Result == IDTEMVData.StartTransSuccess)
//                    {
//                        //Auto Authenticate
//                        myVP3300Reader.Emv_authenticateTransaction(null);
//                    }
//                    else
//                    {
//                        await System.Threading.Tasks.Task.Delay(2000);
//                        Log.Info("EmvTransactionData", "Transaction completed");
//                        transactionCompleted = true;
//                        ReleaseDevice();
//                    }
//                }
//            }
//        }

//        public static byte[] ObjectToByteArray(object obj)
//        {

//            BinaryFormatter bf = new BinaryFormatter();
//            using (var ms = new MemoryStream())
//            {
//                bf.Serialize(ms, obj);
//                return ms.ToArray();
//            }
//        }

//        public void LcdDisplay(int mode, string[] lines, int timeout)
//        {
//            hanlder.Post(async () =>
//            {
//                Log.Info("LcdDisplay", lines[0]);
//                if (mode == 0x01) //Menu Display
//                {
//                    //automatically select 1st application
//                    myVP3300Reader.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
//                }
//                else if (mode == 0x08) //Language Menu Display
//                {
//                    //automatically select first language
//                    myVP3300Reader.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
//                }
//                else if (lines[0].ToLower().Contains("timeout"))
//                {
//                    ResDataStruct toData = new ResDataStruct();
//                    welcomeText.Text = "Feedback: " + lines[0];
//                    await System.Threading.Tasks.Task.Delay(2000);
//                    ReleaseDevice();
//                }
//                else
//                {
//                    welcomeText.Text = "Feedback: " + lines[0];
//                }
//            });
//        }

//        public void TransactionFeedBack(int p0, string[] p1, int p2, byte[] p3, sbyte p4)
//        {
//            hanlder.Post(() =>
//            {
//                Log.Info("TransactionFeedBack", p1[0]);
//                welcomeText.Text = "TransactionFeedBack: " + p1[0];
//            });
//        }

//        public void MsgAudioVolumeAjustFailed()
//        {
//            throw new NotImplementedException();
//        }

//        public void MsgBatteryLow()
//        {
//            throw new NotImplementedException();
//        }

//        public void MsgRKICompleted(string p0)
//        {
//            throw new NotImplementedException();
//        }

//        public void MsgToConnectDevice()
//        {
//            throw new NotImplementedException();
//        }

//        public void SwipeMSRData(IDTMSRData msrData)
//        {
//            hanlder.Post(async () =>
//            {
//                await EmvDataHanldler(msrData);
//            });
//        }

//        public void Timeout(int erroCode)
//        {
//            welcomeText.Text = "Timeout - message: " + ErrorCodeInfo.GetErrorCodeDescription(erroCode);
//            Log.Info("Timeout", "Message..." + ErrorCodeInfo.GetErrorCodeDescription(erroCode));
//            var message = ErrorCodeInfo.GetErrorCodeDescription(erroCode).ToLower();
//            switch (message)
//            {
//                case "card inserted":
//                    break;
//                case "0xee40: unknown error code":
//                case "0x0018: timeout: msr swipe":
//                    ReleaseDevice();
//                    break;

//            }
//            //hanlder.PostDelayed(() => {
//            //    ReleaseDevice();
//            //}, 5000);

//        }

//        void ReleaseDevice()
//        {
//            Log.Info("ReleaseDevice", "Releasing...");
//            if (myVP3300Reader != null)
//            {

//                myVP3300Reader.UnregisterListen();
//                myVP3300Reader.Release();
//                myVP3300Reader = null;
//            }
//        }

//        void RetryTransaction()
//        {
//            myVP3300Reader.Device_cancelTransaction();
//            var errorCode = myVP3300Reader.Device_startTransaction(1, 0, 0, 30, null);
//            var message = myVP3300Reader.Device_getResponseCodeString(errorCode);
//            welcomeText.Text += "Transaction Results: " + message;
//        }

//        public class BLEGattCallBack : BluetoothGattCallback
//        {
//            public override void OnServicesDiscovered(BluetoothGatt gatt, [GeneratedEnum] GattStatus status)
//            {
//                base.OnServicesDiscovered(gatt, status);
//            }

//            public override void OnConnectionStateChange(BluetoothGatt gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
//            {
//                base.OnConnectionStateChange(gatt, status, newState);
//            }
//        }
//        public class BLEScanCallback : Android.Bluetooth.LE.ScanCallback
//        {
//            const string OUI = "00:1C:97"; // IDtech's MAC Address Organizationally Unique Identifier
//            public Action<IList<ParcelUuid>> RegisterBLE { get; set; }
//            public bool _registered = false;

//            public override void OnBatchScanResults(IList<ScanResult> results)
//            {
//                // not used 
//            }

//            public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult r)
//            {
//                //System.Diagnostics.Debug.WriteLine("*** Bluetooth LE Device found - Name: {0}, MAC: {1} ", result.ScanRecord.DeviceName ?? result.Device.Name ?? "No Name", result.Device.Address);

//                //if (!string.IsNullOrEmpty(result.Device.Name) && result.Device.Name.ToUpper().Contains("IDTECH"))
//                //if (result.Device.Address == "00:1C:97:18:77:34")
//                hanlder.Post(() =>
//                {
//                    Log.Info("OnScanResult", "Device..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Address));
//                    Log.Info("OnScanResult", "Device Name..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Name));
//                    System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
//                    System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);

//                    if (bleScanner != null && _registered)
//                        bleScanner.StopScan(mLeScanCallback);

//                    if (r.Device.Address.StartsWith(OUI))
//                    {
//                        AddDeviceToFilter(r);

//                        if (!_registered)
//                        {
//                            System.Diagnostics.Debug.WriteLine("**************FOUND*************");
//                            System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
//                            System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);
//                            System.Diagnostics.Debug.WriteLine("    tXPower " + r.ScanRecord.TxPowerLevel);
//                            System.Diagnostics.Debug.WriteLine("    Rssi " + r.Rssi);

//                            _device = r.Device;
//                            var callBack = new BLEGattCallBack();
//                            gatt = _device.ConnectGatt(Application.Context, true, callBack);

//                            var uuids = r.ScanRecord.ServiceUuids;
//                            Log.Info("OnScanResult", "Device found " + r.Device.Name);
//                            Com.Idtechproducts.Device.Common.BLEDeviceName = r.Device.Name;
//                            BluetoothLEController.SetBluetoothDevice(r.Device);
//                            _registered = true;
//                            RegisterBLE?.Invoke(uuids);
//                            gatt.Connect();
//                        }
//                    }
//                });
//            }

//            private static void AddDeviceToFilter(ScanResult r)
//            {
//                var editor = sharedPre.Edit();
//                string deviceAddress = sharedPre.GetString(DEVICE_ADDRESS, "");

//                if (string.IsNullOrEmpty(deviceAddress))
//                    editor.PutString(DEVICE_ADDRESS, r.Device.Address);
//                else if (!deviceAddress.Contains(r.Device.Address))
//                {
//                    var stringToAdd = string.Format("{0}|{1}", deviceAddress, r.Device.Address);
//                    editor.PutString(DEVICE_ADDRESS, stringToAdd);
//                }
//            }

//            public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
//            {
//                var message = "Bluetooth scan failed.";

//                Log.Info("OnScanFailed", "Error..." + errorCode.ToString());
//                // Feedback?.Invoke(new IDTechEvents { IDTechEvent = IDTechEvents.eIDTechEvents.CardReaderFailure, Value = message });

//                try
//                {
//                    if (errorCode == ScanFailure.ApplicationRegistrationFailed)
//                    {
//                        BluetoothAdapter.DefaultAdapter.Disable();
//                        BluetoothAdapter.DefaultAdapter.Enable();
//                    }
//                }
//                catch (Exception)
//                {
//                }
//            }
//        }
//    }






//}

