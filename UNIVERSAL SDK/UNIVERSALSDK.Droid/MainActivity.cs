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
using Android.Support.V4.Content;
using Android;
using Android.Support.V4.App;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using Com.Idtechproducts.Device.Audiojack.Tools;
using Com.Dbconnection.Dblibrarybeta;
using Android.Database;

namespace UNIVERSALSDK.Droid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, IOnReceiverListener, IRESTResponse, IFirmwareUpdateToolMsg
    {
        private IDT_VP3300 device;
        private BluetoothAdapter mBtAdapter;
        private long BLE_ScanTimeout = 30000; //in milliseconds
        static BLEScanCallback mLeScanCallback;
        static BluetoothLeScanner bleScanner;
        View view;
        static TextView welcomeText, countDown;
        ImageView batteryLifeView;
        static Android.OS.Handler hanlder;
        static bool isScanning, transactionCompleted;
        List<ScanFilter> filters;
        private Handler mStartGattHandler = new Handler();
        static BluetoothDevice _device;
        const string DEVICE_FILTERS = "DeviceFilters", DEVICE_ADDRESS = "DeviceAddress";
        static ISharedPreferences sharedPre;
        static BluetoothGatt gatt;
        BluetoothManager _manager;
        static int devicesScanned;
        BLEGattCallBack bLEGattCallBack;
        System.Timers.Timer _timer;
        private int _countSeconds;
        static ListView devices;
        static List<string> deviceList;
        static ArrayAdapter deviceAdapter;
        static bool shouldTimeout;
        const string OUI = "00:1C:97"; // IDtech's MAC Address Organization\ally Unique Identifier
        FirmwareUpdateTool fwTool;
        ProfileManager profileManager;
        const string FILE_NAME = "Unimag_Cfg.xml";
        bool isFwInitDone;
        bool startSwipe;
        bool btleDeviceRegistered;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            batteryLifeView = (ImageView)FindViewById(Resource.Id.batteryLife);
            welcomeText = (TextView)FindViewById(Resource.Id.welcomeText);
            welcomeText.Text = "Swipe payment";
            countDown = (TextView)FindViewById(Resource.Id.countDown);
            countDown.Visibility = ViewStates.Invisible;

            Log.Info("Initialize", "Init...");

            hanlder = new Handler();
            isScanning = false;
            shouldTimeout = true;
             
            sharedPre = Application.Context.GetSharedPreferences(DEVICE_FILTERS, FileCreationMode.Private);

            filters = filters ?? new List<ScanFilter>();

            bLEGattCallBack = new BLEGattCallBack();

            _timer = new System.Timers.Timer();
           
            _timer.Elapsed -= _timer_Elapsed;
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 1000;

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);

            fab.Click += FabOnClick;

            devices = FindViewById<ListView>(Resource.Id.deviceList);
            deviceList = new List<string>();
            deviceAdapter = new ArrayAdapter<string>(this, Resource.Layout.list_item, deviceList);

            var dso = new DataSetObserverDelegate();

            deviceAdapter.RegisterDataSetObserver(dso);

            devices.Adapter = deviceAdapter;
            devices.Visibility = ViewStates.Invisible;
            profileManager = new ProfileManager(this);

            _manager = (BluetoothManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService);

            mBtAdapter = BluetoothAdapter.DefaultAdapter;

            bleScanner = mBtAdapter.BluetoothLeScanner;

            if (device != null)
                device.UnregisterListen();

            InitializeReader();
        }

        void InitializeReader()
        {
            if (device != null)
            {
                releaseSDK();
            }

            device = new IDT_VP3300(this, Application.Context);
            //profileManager.DoGet();
            device.Log_setVerboseLoggingEnable(true);
            fwTool = new FirmwareUpdateTool(this, Application.Context);
        }


        protected override void OnDestroy()
        {
            if (device != null)
                device.UnregisterListen();

            if (_device != null)
            {
                var mi = _device.Class.GetMethod("removeBond", null);
                mi.Invoke(_device, null);
            }


            base.OnDestroy();
        }
        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _countSeconds--;

            if (_countSeconds >= 0)
            {
                string msg = "Remaining time {0}";

                if (_countSeconds == 0)
                    msg = "Remaining time {0} \n Total Devices found {1}";

                countDown.Text = string.Format(msg, _countSeconds, deviceAdapter.Count);

                System.Diagnostics.Debug.WriteLine("Counting..." + _countSeconds);
            }

            if (_countSeconds <= 0 || (_device != null && _manager.GetConnectionState(_device, ProfileType.Gatt) == ProfileState.Connected))
                _timer.Stop();
        }


        bool _lookSwipe;
        /// <summary>
        /// Start Scan button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void FabOnClick(object sender, EventArgs eventArgs)
        {

            try
            {
                if (_lookSwipe || isScanning) return;

                _lookSwipe = true;

                view = (View)sender;
                Snackbar.Make(view, "Scanning...", Snackbar.LengthLong)
                    .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();

                welcomeText.Text = "Scanning... please wait.";
                transactionCompleted = false;

                _countSeconds = Convert.ToInt32(BLE_ScanTimeout / 1000);
                devicesScanned = 0;
                isScanning = false;
                shouldTimeout = true;

                _timer = new System.Timers.Timer();
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Elapsed += _timer_Elapsed;
                _timer.Interval = 1000;
                btleDeviceRegistered = false;

                countDown.Text = string.Format("Remaining time {0}", _countSeconds);

                if (device.Device_setDeviceType(DEVICE_TYPE.DeviceVp3300Bt))
                {
                    var REQUEST_ENABLE_BT = 1;

                    if (!mBtAdapter.IsEnabled)
                    {
                        var enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                        StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
                    }

                    deviceList.Clear();

                    deviceAdapter.Clear();
                    
                    deviceAdapter.NotifyDataSetChanged();

                    mLeScanCallback = new BLEScanCallback() { RegisterBLE = RegisterBLE, _registered = false };

                    scanLeDevice(true, BLE_ScanTimeout);
    
                }
            }
            catch (Exception ex)
            {
                welcomeText.Text = ex.Message;
            }
            finally
            {
                _lookSwipe = false;
            }
        }

        private void scanLeDevice(bool enable, long timeout)
        {
            hanlder.Post(() =>
            {
                
                _timer.Start();
                countDown.Visibility = ViewStates.Visible;

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
                
                Log.Info("scanLeDevice", "Scanning...");
                batteryLifeView.Visibility = ViewStates.Invisible;

                handler.PostDelayed(() =>
                {
                    if (device != null && !device.Device_isConnected() && !transactionCompleted && shouldTimeout)
                    {
                        welcomeText.Text = "Timeout - Devices Scanned: " + devicesScanned; ;
                        Snackbar.Make(view, "Timeout " + devicesScanned + "- Devices", Snackbar.LengthLong)
                    .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
                        if (isScanning)
                        {
                            bleScanner.StopScan(mLeScanCallback);
                            isScanning = false;
                            gatt?.Disconnect();
                        }
                    }
                }, timeout);


                if (_device != null && _manager.GetConnectionState(_device, ProfileType.Gatt) == ProfileState.Connected)
                {
                    welcomeText.Text = "Connecting... \n Device: " + _device.Name;
                    RegisterBLE(null);
                }
                else if (_manager.Adapter.BondedDevices.Any(x => x.Address.StartsWith(OUI) || !string.IsNullOrEmpty(Common.BLEDeviceName)))
                {
                    _device = _manager.Adapter.BondedDevices.FirstOrDefault(x => x.Address.StartsWith(OUI));
                    var deviceState = _manager.GetConnectionState(_device, ProfileType.Gatt);

                    if (_device != null)
                    {
                        welcomeText.Text = "Connecting... \n Device: " + _device.Name; ;
                        RegisterBLE(null);
                    }
                }
                else if (enable)
                {

                    welcomeText.Text = "Scanning...";

                    var scanFilter = new ScanFilter.Builder().Build();
                    bleScanner.FlushPendingScanResults(mLeScanCallback);
                    ScanSettings settings = new ScanSettings
                                                 .Builder()
                                                 .SetCallbackType(ScanCallbackType.AllMatches)
                                                 .SetMatchMode(BluetoothScanMatchMode.Sticky)
                                                 .SetScanMode(Android.Bluetooth.LE.ScanMode.LowPower)
                                                 .Build();

                    isScanning = true;
                    var success = _manager.Adapter.StartDiscovery();
                    devicesScanned = 0;
                    bleScanner.StartScan(new List<ScanFilter> { scanFilter }, settings, mLeScanCallback);
                }
                else if(isScanning)
                {
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

        public void RegisterBLE(IList<ParcelUuid> uuids)
        {
            Log.Info("RegisterBLE", "RegisterBLE");
            isScanning = false;

            //gatt = _device.ConnectGatt(Application.Context, false, bLEGattCallBack);

            device = device?? new IDT_VP3300(this, Application.Context);

            if (!btleDeviceRegistered)
            {
                device.RegisterListen();
                btleDeviceRegistered = true;
            }

            //gatt.Connect();
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

        private IList<ScanFilter> GetFilters()
        {
            IList<ScanFilter> result = new List<ScanFilter>();

            try
            {
                string adddressStr = sharedPre.GetString(DEVICE_ADDRESS, "");

                string[] addresses = adddressStr.Split("|");

                foreach (var add in addresses)
                {
                    ScanFilter sf = new ScanFilter.Builder()
                        .SetDeviceAddress(_device.Address)
                        .Build();

                    result.Add(sf);
                }
            }
            catch (Exception ex)
            {

                Log.Error("GetFilters", ex.Message);
            }

            return result;
        }

        public void ICCNotifyInfo(byte[] dataNotify, string strMessage)
        {
            if (strMessage != null && strMessage.Length > 0)
            {
                string strHexResp = Common.GetHexStringFromBytes(dataNotify);
                Log.Info("Demo Info >>>>>", "dataNotify=" + strHexResp);
               welcomeText.Text = "ICC Notification Info: " + strMessage + "\n" + "Resp: " + strHexResp;
            }
        }

        public void LoadXMLConfigFailureInfo(int index, string strMessage)
        {
            hanlder.Post(() =>
            {
                welcomeText.Text = "XML loading error...";
            });
        }

        public void AutoConfigCompleted(StructConfigParameters profile)
        {
            profileManager.DoPost(profile);
            string info = "";

            if (device.Device_connectWithProfile(profile))
                info = "Auto Configuration was success";
            else
                info = "Auto Configuration didnt work";

            welcomeText.Text = info;
        }

        public void AutoConfigProgress(int progressValue)
        {
            welcomeText.Text = "AutoConfig is running..." + progressValue + "%";
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
                var sb = new Java.Lang.StringBuilder();
                string info = "";
                var version = device.Device_getFirmwareVersion(sb);
                welcomeText.Text = string.Format("Reader Connected \n firmeware:{0}", sb.ToString());


                Log.Info("Reader", "Connected");
                Log.Info("Device", welcomeText.Text);

                if (!Common.BootLoaderMode)
                {
                    var data = new ResDataStruct();
                    var result = device.Device_sendDataCommand("F002", false, null, data);
                    if (data.ResData != null && data.ResData.Count > 0)
                    {
                        var batteryLevel = data.ResData[0];
                        string level = "Medium";
                        if (batteryLevel >= 210)
                            level = "Full";
                        else if (batteryLevel <= 192)
                            level = "Low";

                        string device_name = device.Device_getDeviceType().ToString();
                        info = device_name.Replace("DEVICE_", "");

                        if (info.StartsWith("VP3300_BT Reader"))
                            info += "Address: " + _device.Address;

                        int image = level == "Full" ? Resource.Mipmap.full : level == "Low" ? Resource.Mipmap.low : Resource.Mipmap.medium;
                        batteryLifeView.SetImageResource(image);
                        await System.Threading.Tasks.Task.Delay(5000);
                    }

                    batteryLifeView.Visibility = ViewStates.Visible;
                    countDown.Visibility = ViewStates.Invisible;

                    welcomeText.Text = info;

                    StartTransaction();
                }
            });
        }

        private void StartTransaction(bool useEMV = false)
        {
            string info = string.Empty;
            startSwipe = true;

            if (useEMV)
            {
                ResDataStruct resData = new ResDataStruct();
                info = "Processing EMV Transaction.  Please wait...\n";
                detail = "";
                int ret = startEMVTransaction(resData);

                if (ret == ErrorCode.ReturnCodeOkNextCommand)
                {
                    info =  "Processing EMV Transaction...";
                }
                else
                {
                    info = "EMV Transaction Failed\n";
                    info += "Status: " + device.Device_getResponseCodeString(ret) + "";
                }
            }
            else
            {
                var ret = device.Device_startTransaction(1, 0, 0, 30, null, true);

                if (ret == ErrorCode.Success)
                {
                    info = "Please swipe/tap/insert a card";
                    detail = "";
                }
                else if (ret == ErrorCode.ReturnCodeOkNextCommand)
                {
                    info = "Start EMV transaction\n";
                    detail = "";
                }
                else
                {
                    info = "Cannot swipe/tap/insert card\n";
                    info += "Status: " + device.Device_getResponseCodeString(ret) + "";
                    detail = "";
                }
            }

            welcomeText.Text += "Transaction Results: " + info;
        }

        ////////////// CALLBACKS /////////////
        public void DeviceDisconnected()
        {
            hanlder.Post(() =>
            {
                Log.Info("Reader", "Disconnected");

                //if (!isScanning && _device != null && _manager.GetConnectionState(_device, ProfileType.Gatt) != ProfileState.Connected)
                //{

                //    batteryLifeView.Visibility = ViewStates.Invisible;
                //    releaseSDK();
                //}

                var info = "";
                if (!Common.BootLoaderMode)
                {
                    info = "Reader is disconnected";
                    batteryLifeView.Visibility = ViewStates.Invisible;
                }

                welcomeText.Text = info;
            });
        }
        string detail = "";

        public void EmvTransactionData(IDTEMVData emvData)
        {
            hanlder.Post(async () =>
            {
                await EmvDataHanldler(emvData);
            });
        }

        private async System.Threading.Tasks.Task EmvDataHanldler(object dataEntry)
        {
            if (dataEntry.GetType() == typeof(IDTEMVData))
            {
                var emvData = dataEntry as IDTEMVData;
                if (emvData != null)
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
                            var d = emvData.UnencryptedTags[key.Key];

                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }
                    if (emvData.MaskedTags != null && emvData.MaskedTags.Count > 0)
                    {
                        detail += "Masked Tags:nrnn";
                        foreach (DictionaryEntry key in emvData.MaskedTags)
                        {
                            detail += key + ": ";
                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }
                    if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count > 0)
                    {
                        detail += "Encrypted Tags:nrnn";
                        foreach (DictionaryEntry key in emvData.EncryptedTags)
                        {
                            detail += key + ": ";
                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }

                    welcomeText.Text = detail;
                    Log.Info("EmvTransactionData", detail);

                    var responseString = device.Device_getResponseCodeString(emvData.Result);

                    if (emvData.Result == IDTEMVData.GoOnline)
                    {
                        //Auto Complete
                        byte[] response = new byte[] { 0x30, 0x30 };
                        device.Emv_completeTransaction(false, response, null, null, null);
                    }
                    else if (emvData.Result == IDTEMVData.StartTransSuccess)
                    {
                        //Auto Authenticate
                        device.Emv_authenticateTransaction(null);
                    }
                    else
                    {
                        welcomeText.Text = "Transaction completed";
                        await System.Threading.Tasks.Task.Delay(2000);
                        Log.Info("EmvTransactionData", "Transaction completed");
                        transactionCompleted = true;
                        //ReleaseDevice();
                    }
                }
            }
            else if (dataEntry.GetType() == typeof(IDTMSRData))
            {
                var emvData = dataEntry as IDTMSRData;

                if (emvData != null)
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
                            var d = emvData.UnencryptedTags[key.Key];

                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }
                    if (emvData.MaskedTags != null && emvData.MaskedTags.Count > 0)
                    {
                        detail += "Masked Tags:nrnn";
                        foreach (DictionaryEntry key in emvData.MaskedTags)
                        {
                            detail += key + ": ";
                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }
                    if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count > 0)
                    {
                        detail += "Encrypted Tags:nrnn";
                        foreach (DictionaryEntry key in emvData.EncryptedTags)
                        {
                            detail += key + ": ";
                            byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                            detail += Common.GetHexStringFromBytes(data) + "nrnn";
                        }
                    }

                    welcomeText.Text = detail;
                    Log.Info("EmvTransactionData", detail);

                    if (emvData.Result == IDTEMVData.GoOnline)
                    {
                        //Auto Complete
                        byte[] response = new byte[] { 0x30, 0x30 };
                        device.Emv_completeTransaction(false, response, null, null, null);
                    }
                    else if (emvData.Result == IDTEMVData.StartTransSuccess)
                    {
                        //Auto Authenticate
                        device.Emv_authenticateTransaction(null);
                    }
                    else if (emvData.Result == 10)
                    {
                        welcomeText.Text = "Please Swipe or insert card";
                        StartTransaction();
                    }
                    else if (emvData.Result == IDTEMVData.ApprovedOffline)
                    {
                        welcomeText.Text = "USE CHIP READER";
                        StartTransaction();
                    }
                    else
                    {
                        welcomeText.Text = "Transaction completed";
                        await System.Threading.Tasks.Task.Delay(2000);
                        Log.Info("EmvTransactionData", "Transaction completed");
                        transactionCompleted = true;
                        //ReleaseDevice();
                    }
                }
            }
        }

        public static byte[] ObjectToByteArray(object obj)
        {

            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public void LcdDisplay(int mode, string[] lines, int timeout)
        {
            hanlder.Post(async () =>
            {
                welcomeText.Text = lines[0];
                Log.Info("LcdDisplay", lines[0]);

                if (mode == 0x01) //Menu Display
                {
                    //automatically select 1st application
                    device.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
                }
                else if (mode == 0x08) //Language Menu Display
                {
                    //automatically select first language
                    device.Emv_lcdControlResponse((sbyte)mode, (sbyte)0x01);
                }
                else if (lines[0].ToLower().Contains("timeout"))
                {
                    ResDataStruct toData = new ResDataStruct();
                    welcomeText.Text = "Feedback: " + lines[0];
                    await System.Threading.Tasks.Task.Delay(2000);
                    //ReleaseDevice();
                }
            });
        }

        public void TransactionFeedBack(int p0, string[] p1, int p2, byte[] p3, sbyte p4)
        {
            hanlder.Post(() =>
            {
                Log.Info("TransactionFeedBack", p1[0]);
                System.Diagnostics.Debug.WriteLine(string.Format("*** po {0} \n p1 {1}, p2 {2}, p3 {3}, p4 {4} ***", p0, p1.ToString(), p2, p3.ToString(), p4 ));
                welcomeText.Text =  p1[0];
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
            welcomeText.Text =  "Connecting a reader...";
        }

        public void SwipeMSRData(IDTMSRData msrData)
        {
            hanlder.Post(async () =>
            {
                await EmvDataHanldler(msrData);
            });
        }

        public void Timeout(int erroCode)
        {
            welcomeText.Text = "Timeout - message: " + ErrorCodeInfo.GetErrorCodeDescription(erroCode) + " Devices scanned: " + devicesScanned;
            Log.Info("Timeout", "Message..." + ErrorCodeInfo.GetErrorCodeDescription(erroCode));
            var message = ErrorCodeInfo.GetErrorCodeDescription(erroCode).ToLower();
            switch (message)
            {
                case "card inserted":
                    break;
                case "0xee40: unknown error code":
                case "0x0018: timeout: msr swipe":
                    //ReleaseDevice();
                    break;

            }
            //hanlder.PostDelayed(() => {
            //    ReleaseDevice();
            //}, 5000);

        }

        void releaseSDK()
        {
            Log.Info("ReleaseDevice", "Releasing...");
            if (device != null)
            {
                device.UnregisterListen();
                device.Release();
            }
        }

        void RetryTransaction()
        {
            device.Device_cancelTransaction();
            var errorCode = device.Device_startTransaction(1, 0, 0, 30, null);
            var message = device.Device_getResponseCodeString(errorCode);
            welcomeText.Text += "Transaction Results: " + message;
        }

        public void GetProfileResult(string output)
        {
            if (output.Equals("404"))
            {
                welcomeText.Text = "Profile not found. trying xml";
                
                String filepath = getXMLFileFromRaw();
                if (!File.Exists(filepath))
                {
                    filepath = null;
                }
                device.Config_setXMLFileNameWithPath(filepath);
                device.Config_loadingConfigurationXMLFile(false);
            }
            else
            {
                device.Device_connectWithProfile(ProfileUtility.JSONtoProfile(output));
                welcomeText.Text = "Profile not found. trying xml";
            }
        }

        public void PostProfileResult(string s)
        {
            welcomeText.Text = "Post: " + s;
        }

        string getXMLFileFromRaw()
        {
            string szFilenameWithPath = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath, FILE_NAME);

            if (!File.Exists(szFilenameWithPath))
            {
                using (var in_stream = Application.Context.Resources.OpenRawResource(Resource.Raw.umcfg))
                {
                    using (var out_stream = File.Create(szFilenameWithPath))
                    {
                        in_stream.CopyTo(out_stream);
                        out_stream.Flush();
                    }
                }
            }

            return szFilenameWithPath;
        }

        public void OnReceiveMsgChallengeResult(int returnCode, byte[] data)
        {
            // Not called for UniPay Firmware update
        }

        public void OnReceiveMsgUpdateFirmwareProgress(int nProgressValue)
        {
            string info;

            if (Common.BootLoaderMode)
                info = "Firmware update is in process... (" + nProgressValue + "%)";
            else
                info = "Firmware update initialization is in process...";
            welcomeText.Text += "\n" + info;
        }

        public int startEMVTransaction(ResDataStruct resData)
        {
            byte[] tags = { (byte)0xDF, (byte)0xEF, 0x1F, 0x02, 0x01, 0x00 };
            IDT_VP3300.Emv_allowFallback(true);
            if (IDT_VP3300.Emv_getAutoAuthenticateTransaction())
                return device.Emv_startTransaction(1, 0.00, 0, 30, tags, false);
            else
                return device.Emv_startTransaction(1, 0.00, 0, 30, null, false);
        }
        public void OnReceiveMsgUpdateFirmwareResult(int result)
        {
            string info = string.Empty;
            switch (result)
            {
                case FirmwareUpdateToolMsg.CmdUpdateFirmwareSucceed:
                    info = "Firmware update is done successfully...";
                    isFwInitDone = false;
                    break;
                case FirmwareUpdateToolMsg.CmdInitializeUpdateFirmwareSucceed:
                    if (device.Device_getDeviceType() == DEVICE_TYPE.DeviceKioskIii)
                        info = "Firmware update initialization is done successfully, please wait for device reconnection and do firmware update.";
                    else
                        info = "Firmware update initialization is done successfully, please do firmware update now.";
                    isFwInitDone = true;
                    break;
                case FirmwareUpdateToolMsg.CmdUpdateFirmwareTimeout:
                    if (Common.BootLoaderMode)
                    {
                        info = "Firmware update timeout... Please try firmware update again...";
                        detail = "";
                    }
                    else
                    {
                        info = "Firmware update initialization timeout... Please try again...";
                    }
                    break;
                case FirmwareUpdateToolMsg.CmdUpdateFirmwareDownloadBlockFailed:
                    info = "Firmware update failed... Please try again...";
                    detail = "";
                    break;
            }

            if (string.IsNullOrEmpty(info))
                welcomeText.Text = info;
        }

        private void printTags(IDTEMVData emvData)
        {
            if (emvData.Result == IDTEMVData.StartTransSuccess)
                detail = "Start transaction response:\r\n";
            else if (emvData.Result == IDTEMVData.GoOnline)
                detail += "\r\nAuthentication response:\r\n";
            else if (emvData.Result == IDTEMVData.UseMagstripe || emvData.Result == IDTEMVData.MsrSuccess)
            {
                swipeMSRData(emvData.MsrCardData);
                detail += "\r\n\r\n";
                detail += this.emvErrorCodes(emvData.Result) + "\r\n";
                return;
            }
            else
                detail += "\r\nComplete Transaction response:\r\n";
            if (emvData.UnencryptedTags != null && emvData.UnencryptedTags.Count != 0)
            {
                detail += "Unencrypted Tags:\r\n";
                var keys = emvData.UnencryptedTags.Keys;
                foreach (DictionaryEntry key in keys)
                {
                    detail += key + ": ";
                    var d = emvData.UnencryptedTags[key.Key];
                    byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                    detail += Common.GetHexStringFromBytes(data) + "\r\n";
                }
            }
            if (emvData.MaskedTags != null && emvData.MaskedTags.Count != 0)
            {
                detail += "Masked Tags:\r\n";
                var keys = emvData.MaskedTags.Keys;
                foreach (DictionaryEntry key in keys)
                {
                    detail += key + ": ";
                    var d = emvData.MaskedTags[key.Key];
                    byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                    detail += Common.GetHexStringFromBytes(data) + "\r\n";
                }
            }
            if (emvData.EncryptedTags != null && emvData.EncryptedTags.Count != 0)
            {
                detail += "Encrypted Tags:\r\n";
                var keys = emvData.EncryptedTags.Keys;
                foreach (DictionaryEntry key in keys)
                {
                    detail += key + ": ";
                    var d = emvData.EncryptedTags[key.Key];
                    byte[] data = Encoding.ASCII.GetBytes(key.Key.ToString());
                    detail += Common.GetHexStringFromBytes(data) + "\r\n";
                }
            }
            if (emvData.MsrCardData != null && emvData.MsrCardData.FastEMV != null && emvData.MsrCardData.FastEMV.Length > 0)
                detail += "\r\nFastEMV: " + emvData.MsrCardData.FastEMV + "\r\n\r\n";
            detail += this.emvErrorCodes(emvData.Result) + "\r\n";

            welcomeText.Text = detail;
        }
        IDTMSRData msr_card;
        public void swipeMSRData(IDTMSRData card)
        {
            msr_card = card;
            string info = "";
            hanlder.Post(() => {
                startSwipe = false;
                if (msr_card.Result != ErrorCode.Success)
                {
                    info = "MSR card data didn't read correctly\n";
                    info += ErrorCodeInfo.GetErrorCodeDescription(msr_card.Result);
                    if (msr_card.Result != ErrorCode.FailedNack)
                    {
                        detail = "";
                        welcomeText.Text = info;
                        return;
                    }
                }
                else
                {
                    info = "MSR Card tapped/Swiped Successfully";
                }

                detail = Common.Parse_MSRData(device.Device_getDeviceType(), msr_card);

                welcomeText.Text = info + "\n details" + detail;

            });
        }
        private string emvErrorCodes(int val)
        {
            if (val == IDTEMVData.ApprovedOffline) return "APPROVED OFFLINE";
            if (val == IDTEMVData.DeclinedOffline) return "DECLINED OFFLINE";
            if (val == IDTEMVData.Approved) return "APPROVED";
            if (val == IDTEMVData.Declined) return "DECLINED";
            if (val == IDTEMVData.GoOnline) return "GO ONLINE";
            if (val == IDTEMVData.CallYourBank) return "CALL YOUR BANK";
            if (val == IDTEMVData.NotAccepted) return "NOT ACCEPTED";
            if (val == IDTEMVData.UseMagstripe) return "USE MAGSTRIPE";
            if (val == IDTEMVData.TimeOut) return "TIME OUT";
            if (val == IDTEMVData.StartTransSuccess) return "START TRANS SUCCESS";
            if (val == IDTEMVData.MsrSuccess) return "MSR SUCCESS";
            if (val == IDTEMVData.TransactionCanceled) return "TRANSACTION CANCELED";
            if (val == IDTEMVData.CtlsTwoCards) return "CTLS TWO CARDS";
            if (val == IDTEMVData.CtlsTerminate) return "CTLS TERMINATE";
            if (val == IDTEMVData.CtlsTerminateTryAnother) return "CTLS TERMINATE TRY ANOTHER";
            if (val == IDTEMVData.MsrSwipeCaptured) return "MSR SWIPE CAPTURED";
            if (val == IDTEMVData.RequestOnlinePin) return "REQUEST ONLINE PIN";
            if (val == IDTEMVData.RequestSignature) return "REQUEST SIGNATURE";
            if (val == IDTEMVData.FallbackToContact) return "FALLBACK TO CONTACT";
            if (val == IDTEMVData.FallbackToOther) return "FALLBACK TO OTHER";
            if (val == IDTEMVData.ReversalRequired) return "REVERSAL REQUIRED";
            if (val == IDTEMVData.AdviseRequired) return "ADVISE REQUIRED";
            if (val == IDTEMVData.NoAdviseReversalRequired) return "NO ADVISE REVERSAL REQUIRED";
            if (val == IDTEMVData.UnableToReachHost) return "UNABLE TO REACH HOST";
            if (val == IDTEMVData.FileArgInvalid) return "FILE ARG INVALID";
            if (val == IDTEMVData.FileOpenFailed) return "FILE OPEN FAILED";
            if (val == IDTEMVData.FileOperationFailed) return "FILE OPERATION FAILED";
            if (val == IDTEMVData.MemoryNotEnough) return "MEMORY NOT ENOUGH";
            if (val == IDTEMVData.SmartcardOk) return "SMARTCARD OK";
            if (val == IDTEMVData.SmartcardFail) return "SMARTCARD FAIL";
            if (val == IDTEMVData.SmartcardInitFailed) return "SMARTCARD INIT FAILED";
            if (val == IDTEMVData.FallbackSituation) return "FALLBACK SITUATION";
            if (val == IDTEMVData.SmartcardAbsent) return "SMARTCARD ABSENT";
            if (val == IDTEMVData.SmartcardTimeout) return "SMARTCARD TIMEOUT";
            if (val == IDTEMVData.MsrCardError) return "MSR CARD ERROR";
            if (val == IDTEMVData.ParsingTagsFailed) return "PARSING TAGS FAILED";
            if (val == IDTEMVData.CardDataElementDuplicate) return "CARD DATA ELEMENT DUPLICATE";
            if (val == IDTEMVData.DataFormatIncorrect) return "DATA FORMAT INCORRECT";
            if (val == IDTEMVData.AppNoTerm) return "APP NO TERM";
            if (val == IDTEMVData.AppNoMatching) return "APP NO MATCHING";
            if (val == IDTEMVData.AmandatoryObjectMissing) return "AMANDATORY OBJECT MISSING";
            if (val == IDTEMVData.AppSelectionRetry) return "APP SELECTION RETRY";
            if (val == IDTEMVData.AmountErrorGet) return "AMOUNT ERROR GET";
            if (val == IDTEMVData.CardRejected) return "CARD REJECTED";
            if (val == IDTEMVData.AipNotReceived) return "AIP NOT RECEIVED";
            if (val == IDTEMVData.AflNotReceivede) return "AFL NOT RECEIVEDE";
            if (val == IDTEMVData.AflLenOutOfRange) return "AFL LEN OUT OF RANGE";
            if (val == IDTEMVData.SfiOutOfRange) return "SFI OUT OF RANGE";
            if (val == IDTEMVData.AflIncorrect) return "AFL INCORRECT";
            if (val == IDTEMVData.ExpDateIncorrect) return "EXP DATE INCORRECT";
            if (val == IDTEMVData.EffDateIncorrect) return "EFF DATE INCORRECT";
            if (val == IDTEMVData.IssCodTblOutOfRange) return "ISS COD TBL OUT OF RANGE";
            if (val == IDTEMVData.CryptogramTypeIncorrect) return "CRYPTOGRAM TYPE INCORRECT";
            if (val == IDTEMVData.PseByCardNotSupported) return "PSE BY CARD NOT SUPPORTED";
            if (val == IDTEMVData.UserLanguageSelected) return "USER LANGUAGE SELECTED";
            if (val == IDTEMVData.ServiceNotAllowed) return "SERVICE NOT ALLOWED";
            if (val == IDTEMVData.NoTagFound) return "NO TAG FOUND";
            if (val == IDTEMVData.CardBlocked) return "CARD BLOCKED";
            if (val == IDTEMVData.LenIncorrect) return "LEN INCORRECT";
            if (val == IDTEMVData.CardComError) return "CARD COM ERROR";
            if (val == IDTEMVData.TscNotIncreased) return "TSC NOT INCREASED";
            if (val == IDTEMVData.HashIncorrect) return "HASH INCORRECT";
            if (val == IDTEMVData.ArcNotPresenced) return "ARC NOT PRESENCED";
            if (val == IDTEMVData.ArcInvalid) return "ARC INVALID";
            if (val == IDTEMVData.CommNoOnline) return "COMM NO ONLINE";
            if (val == IDTEMVData.TranTypeIncorrect) return "TRAN TYPE INCORRECT";
            if (val == IDTEMVData.AppNoSupport) return "APP NO SUPPORT";
            if (val == IDTEMVData.AppNotSelect) return "APP NOT SELECT";
            if (val == IDTEMVData.LangNotSelect) return "LANG NOT SELECT";
            if (val == IDTEMVData.TermDataNotPresenced) return "TERM DATA NOT PRESENCED";
            if (val == IDTEMVData.CvmTypeUnknown) return "CVM TYPE UNKNOWN";
            if (val == IDTEMVData.CvmAipNotSupported) return "CVM AIP NOT SUPPORTED";
            if (val == IDTEMVData.CvmTag8eMissing) return "CVM TAG 8E MISSING";
            if (val == IDTEMVData.CvmTag8eFormatError) return "CVM TAG 8E FORMAT ERROR";
            if (val == IDTEMVData.CvmCodeIsNotSupported) return "CVM CODE IS NOT SUPPORTED";
            if (val == IDTEMVData.CvmCondCodeIsNotSupported) return "CVM COND CODE IS NOT SUPPORTED";
            if (val == IDTEMVData.CvmNoMore) return "CVM NO MORE";
            if (val == IDTEMVData.PinBypassedBefore) return "PIN BYPASSED BEFORE";
            if (val == IDTEMVData.Unkonwn) return "UNKONWN";
            return "";
        }

        public class DataSetObserverDelegate : DataSetObserver
        {
            public override void OnChanged()
            {
                base.OnChanged();
                devices.SetSelection(deviceAdapter.Count - 1);
            }
        }
        public class BLEGattCallBack : BluetoothGattCallback
        {
            public override void OnServicesDiscovered(BluetoothGatt gatt, [GeneratedEnum] GattStatus status)
            {
                base.OnServicesDiscovered(gatt, status);

                //var services = gatt.Services;
                //System.Diagnostics.Debug.WriteLine("*** DISCOVERING SERVICES ***");

                //foreach (var serv in services)
                //{
                //    System.Diagnostics.Debug.WriteLine("    Service Id " + serv.Uuid);

                //    var chs = serv.Characteristics;

                //    foreach (var ch in chs)
                //    {
                //        System.Diagnostics.Debug.WriteLine("        characteristic Id " + ch.Uuid);
                //        System.Diagnostics.Debug.WriteLine("          Permissions " + ch.Permissions);

                //        foreach (var de in ch.Descriptors)
                //        {
                //            System.Diagnostics.Debug.WriteLine("          Descriptor Id " + de.Uuid);
                //            System.Diagnostics.Debug.WriteLine("          Permissions " + de.Permissions);
                //        }
                //        //gatt.SetCharacteristicNotification(ch, true);
                //    }
                //}
            }

            public override void OnConnectionStateChange(BluetoothGatt gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
            {
                base.OnConnectionStateChange(gatt, status, newState);

                //if (newState == ProfileState.Connected)
                //   gatt.DiscoverServices();
            }

            
        }
        public class BLEScanCallback : Android.Bluetooth.LE.ScanCallback
        {
            
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

                Log.Info("OnScanResult", "Device..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Address));
                Log.Info("OnScanResult", "Device Name..." + (string.IsNullOrEmpty(r.Device.Address) ? r.Device.Name : r.Device.Name));
                System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
                System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);

                string device_ = string.IsNullOrEmpty(r.Device.Name) ? r.Device.Address : r.Device.Name;

                if (!deviceList.Contains(device_))
                {
                    deviceList.Add(device_);
                    deviceAdapter.Add(device_);
                    deviceAdapter.NotifyDataSetChanged();
                }

                if (_registered || !isScanning)
                {
                    bleScanner.StopScan(mLeScanCallback);

                    if (_registered)
                        welcomeText.Text = "Device Registered..." + r.Device.Name;
                }
                else
                {
                    devicesScanned++;

                    welcomeText.Text = string.Format("Scanning...{0} \n device address {1}", devicesScanned, r.Device.Address);

                    System.Diagnostics.Debug.WriteLine("    devicesScanned: " + devicesScanned);
                }

                if (r.Device.Address.StartsWith(OUI))
                {
                    //AddDeviceToFilter(r);
                    bleScanner.StopScan(mLeScanCallback);
                    isScanning = false;

                    shouldTimeout = false;

                    if (!_registered)
                    {
                        _registered = true;
                        System.Diagnostics.Debug.WriteLine("**************FOUND*************");
                        System.Diagnostics.Debug.WriteLine("    Name " + r.Device.Name);
                        System.Diagnostics.Debug.WriteLine("    Address " + r.Device.Address);
                        System.Diagnostics.Debug.WriteLine("    tXPower " + r.ScanRecord.TxPowerLevel);
                        System.Diagnostics.Debug.WriteLine("    Rssi " + r.Rssi);

                        if (!deviceList.Contains(device_))
                        {
                            deviceList.Add(device_);
                            deviceAdapter.Add(device_);
                        }

                        welcomeText.Text = "Device Found - " + r.Device.Name;

                        r.Device.CreateBond();

                        Common.BLEDeviceName = r.Device.Address;
                        BluetoothLEController.SetBluetoothDevice(r.Device);
                        _device = r.Device;

                        var uuids = r.ScanRecord.ServiceUuids;

                        RegisterBLE?.Invoke(uuids);

                        deviceAdapter.NotifyDataSetChanged();
                    }
                }
            }

            private static void AddDeviceToFilter(ScanResult r)
            {
                var editor = sharedPre.Edit();
                string deviceAddress = sharedPre.GetString(DEVICE_ADDRESS, "");

                if (string.IsNullOrEmpty(deviceAddress))
                    editor.PutString(DEVICE_ADDRESS, r.Device.Address);
                else if (!deviceAddress.Contains(r.Device.Address))
                {
                    var stringToAdd = string.Format("{0}|{1}", deviceAddress, r.Device.Address);
                    editor.PutString(DEVICE_ADDRESS, stringToAdd);
                }
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

