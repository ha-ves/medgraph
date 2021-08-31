using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Medical_Graph_App
{
    public static class ExtensionMethods
    {
        public static int Remap(this float value, float from_min, float from_max, float to_min, float to_max)
        {
            return (int)((value - from_min) / (from_max - from_min) * (to_max - to_min) + to_min);
        }

        public static int Remap(this int value, float from_min, float from_max, float to_min, float to_max)
        {
            return (int)((value - from_min) / (from_max - from_min) * (to_max - to_min) + to_min);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public class GraphCanvas
        {
            public GraphCanvas(Canvas canvas)
            {
                theCanvas = canvas;

                currPoint = theCanvas.Children.OfType<Polyline>().ElementAt(0).Points;
                otherPoint = theCanvas.Children.OfType<Polyline>().ElementAt(1).Points;
            }

            Canvas theCanvas;
            public Canvas GetCanvas() { return theCanvas; }

            PointCollection currPoint, otherPoint;

            public bool Averaging { get; set; } = false;
            public int MaxValue { get => graph_max; set => graph_max = value; }
            public int MinValue { get => graph_min; set => graph_min = value; }
            public int MaxStep { get => maxStep; set => maxStep = value; }

            int graph_min = 0, graph_max = 100;
            int point_x = 0, maxStep = 200;
            static int sync_point_x = 0;

            bool loop = false, looped = false;
            public void AddPointtoLineGraph(float value)
            {
                if (point_x >= maxStep)
                {
                    point_x = 0;
                    loop = !loop;
                    looped = true;

                    if (Averaging)
                    {
                        var avgpx = (float)currPoint.Average(p => p.Y);
                        Debug.WriteLine($"Avg Value : [ {avgpx} ]");
                        var avgval = (int)avgpx.Remap(0, (float)theCanvas.ActualHeight, graph_min, graph_max);
                        graph_min = graph_max = avgval;
                    }

                    if (loop)
                    {
                        currPoint = theCanvas.Children.OfType<Polyline>().ElementAt(1).Points;
                        otherPoint = theCanvas.Children.OfType<Polyline>().ElementAt(0).Points;
                    }
                    else
                    {
                        currPoint = theCanvas.Children.OfType<Polyline>().ElementAt(0).Points;
                        otherPoint = theCanvas.Children.OfType<Polyline>().ElementAt(1).Points;
                    }
                }

                try {
                    if (point_x > maxStep - (maxStep / 20)) currPoint.Remove(currPoint.FirstOrDefault());
                    if (looped) otherPoint.Remove(otherPoint.FirstOrDefault());
                } catch (Exception e) {
                    Debug.WriteLine($"[REMOVE POINT EXCEPTION] {e.GetType()} \n {e.StackTrace}");
                }

                if (value > graph_max)
                {
                    graph_max = (int)(value + ((graph_max - graph_min) / 5));
                    Debug.WriteLine($"updated graph_max = {graph_max}");
                }
                else if (value < graph_min)
                {
                    graph_min = (int)(value - ((graph_max - graph_min) / 5));
                    Debug.WriteLine($"updated graph_min = {graph_min}");
                }

                var x = point_x.Remap(1, maxStep, 0, 1420);
                var y = value.Remap(graph_min, graph_max, 0, 101);
                point_x++;

                Debug.WriteLine($"Value : {value}");
                Debug.WriteLine($"New Point [ {x} , {y} ]");
                Debug.WriteLine($"canvas actual dimension : {theCanvas.ActualWidth} x {theCanvas.ActualHeight}");
                currPoint.Add(new Point(x, y));
            }
        }

        GraphCanvas Spo2_Graph;
        GraphCanvas[] ECG_Graphs;

        TcpClient tcpClient;
        Thread th;

        public MainWindow()
        {
#if DEBUG
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
#endif
            InitializeComponent();

            DataContext = this;

            Thread.CurrentThread.Name = "Main UI Thread";
            Debug.WriteLine($"The MainWindow Thread : {Thread.CurrentThread.Name}");

            Spo2_Graph = new(spo2_canvas) { MinValue = 70, MaxValue = 80, MaxStep = 150};

            ECG_Graphs = new[] {
                new GraphCanvas(ecg_1_canvas),
                new GraphCanvas(ecg_2_canvas),
                new GraphCanvas(ecg_3_canvas),
                new GraphCanvas(ecg_4_canvas),
                new GraphCanvas(ecg_5_canvas),
                new GraphCanvas(ecg_6_canvas)
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //AddDummyData();
            //Task.Run(() => KoneksiDenganESP());

            ECG_Data();
        }

        private async Task ECG_Data()
        {
            short[] y_data = {
                939, 940, 941, 942, 944, 945, 946, 947, 951, 956,
                962, 967, 973, 978, 983, 989, 994, 1000, 1005, 1015,
                1024, 1034, 1043, 1053, 1062, 1075, 1087, 1100, 1112, 1121,
                1126, 1131, 1136, 1141, 1146, 1151, 1156, 1164, 1172, 1179,
                1187, 1194, 1202, 1209, 1216, 1222, 1229, 1235, 1241, 1248,
                1254, 1260, 1264, 1268, 1271, 1275, 1279, 1283, 1287, 1286,
                1284, 1281, 1279, 1276, 1274, 1271, 1268, 1266, 1263, 1261,
                1258, 1256, 1253, 1251, 1246, 1242, 1237, 1232, 1227, 1222,
                1218, 1215, 1211, 1207, 1203, 1199, 1195, 1191, 1184, 1178,
                1171, 1165, 1159, 1152, 1146, 1141, 1136, 1130, 1125, 1120,
                1115, 1110, 1103, 1096, 1088, 1080, 1073, 1065, 1057, 1049,
                1040, 1030, 1021, 1012, 1004, 995, 987, 982, 978, 974,
                970, 966, 963, 959, 955, 952, 949, 945, 942, 939,
                938, 939, 940, 941, 943, 944, 945, 946, 946, 946,
                946, 946, 946, 946, 946, 947, 950, 952, 954, 956,
                958, 960, 962, 964, 965, 965, 965, 965, 965, 965,
                963, 960, 957, 954, 951, 947, 944, 941, 938, 932,
                926, 920, 913, 907, 901, 894, 885, 865, 820, 733,
                606, 555, 507, 632, 697, 752, 807, 896, 977, 1023,
                1069, 1127, 1237, 1347, 1457, 2085, 2246, 2474, 2549, 2595,
                2641, 2695, 3083, 3135, 3187, 3217, 3315, 3403, 3492, 3581,
                3804, 3847, 3890, 3798, 3443, 3453, 3297, 3053, 2819, 2810,
                2225, 2258, 1892, 1734, 1625, 998, 903, 355, 376, 203,
                30, 33, 61, 90, 119, 160, 238, 275, 292, 309,
                325, 343, 371, 399, 429, 484, 542, 602, 652, 703,
                758, 802, 838, 856, 875, 895, 917, 938, 967, 1016,
                1035, 1041, 1047, 1054, 1060, 1066, 1066, 1064, 1061, 1058,
                1056, 1053, 1051, 1048, 1046, 1043, 1041, 1038, 1035, 1033,
                1030, 1028, 1025, 1022, 1019, 1017, 1014, 1011, 1008, 1006,
                1003, 1001, 999, 998, 996, 994, 993, 991, 990, 988,
                986, 985, 983, 981, 978, 976, 973, 971, 968, 966,
                963, 963, 963, 963, 963, 963, 963, 963, 963, 963,
                963, 963, 963, 963, 963, 963, 963, 963, 963, 963,
                964, 965, 966, 967, 968, 969, 970, 971, 972, 974,
                976, 978, 980, 983, 985, 987, 989, 991, 993, 995,
                997, 999, 1002, 1006, 1011, 1015, 1019, 1023, 1028, 1032,
                1036, 1040, 1045, 1050, 1055, 1059, 1064, 1069, 1076, 1082,
                1088, 1095, 1101, 1107, 1114, 1120, 1126, 1132, 1141, 1149,
                1158, 1166, 1173, 1178, 1183, 1188, 1193, 1198, 1203, 1208,
                1214, 1221, 1227, 1233, 1240, 1246, 1250, 1254, 1259, 1263,
                1269, 1278, 1286, 1294, 1303, 1309, 1315, 1322, 1328, 1334,
                1341, 1343, 1345, 1347, 1349, 1351, 1353, 1355, 1357, 1359,
                1359, 1359, 1359, 1359, 1358, 1356, 1354, 1352, 1350, 1347,
                1345, 1343, 1341, 1339, 1336, 1334, 1332, 1329, 1327, 1324,
                1322, 1320, 1317, 1315, 1312, 1307, 1301, 1294, 1288, 1281,
                1275, 1270, 1265, 1260, 1256, 1251, 1246, 1240, 1233, 1227,
                1221, 1214, 1208, 1201, 1194, 1186, 1178, 1170, 1162, 1154,
                1148, 1144, 1140, 1136, 1131, 1127, 1123, 1118, 1114, 1107,
                1099, 1090, 1082, 1074, 1069, 1064, 1058, 1053, 1048, 1043,
                1038, 1034, 1029, 1025, 1021, 1017, 1013, 1009, 1005, 1001,
                997, 994, 990, 991, 992, 994, 996, 997, 999, 998,
                997, 996, 995, 994, 993, 991, 990, 989, 989, 989,
                989, 989, 989, 989, 988, 986, 984, 983, 981, 980,
                982, 984, 986, 988, 990, 993, 995, 997, 999, 1002,
                1005, 1008, 1012};

            ECG_Graphs[0].MaxValue = 50;
            ECG_Graphs[0].MinValue = -10;
            ECG_Graphs[0].MaxStep = 750;

            for (int i = 0; i < 20; i++)
            {
                for (int j = 100; j < y_data.Length - 200; j += 3)
                {
                    ECG_Graphs[0].AddPointtoLineGraph((float)(y_data[j] * 0.01));
                    await Task.Delay(5);
                }
            }
        }

        PointCollection values = new PointCollection();
        private async Task AddDummyData()
        {
            int[] val = { 102, 117, 131, 132, 123, 114, 105, 96, 88, 87, 102 };
            int[] vel = { 102, 137, 151, 152, 143, 134, 105, 76, 68, 67, 102 };

            string[] ecg_emu = { 
                "*,0,102,102,102,102,102,102#",
                "*,0,117,117,117,117,117,117#",
                "*,0,131,131,131,131,131,131#",
                "*,0,132,132,132,132,132,132#",
                "*,0,123,123,123,123,123,123#",
                "*,0,114,114,114,114,114,114#",
                "*,0,105,105,105,105,105,105#",
                "*,0,96,96,96,96,96,96#",
                "*,0,88,88,88,88,88,88#",
                "*,0,87,87,87,87,87,87#",
                "*,0,102,102,102,102,102,102#"
            };
            string[] ecg_emu1 = {
                "*,1,102,102,102,102,102,102#",
                "*,1,117,117,117,117,117,117#",
                "*,1,131,131,131,131,131,131#",
                "*,1,132,132,132,132,132,132#",
                "*,1,123,123,123,123,123,123#",
                "*,1,114,114,114,114,114,114#",
                "*,1,105,105,105,105,105,105#",
                "*,1,96,96,96,96,96,96#",
                "*,1,88,88,88,88,88,88#",
                "*,1,87,87,87,87,87,87#",
                "*,1,102,102,102,102,102,102#"
            };

            //foreach (var item in vel)
            //{
            //    Spo2_Graph.AddPointtoLineGraph(item);
            //    await Task.Delay(33);
            //}

            //for (int i = 0; i < 20; i++)
            //{
            //    foreach (var item in val)
            //    {
            //        foreach (var graph in ECG_Graphs)
            //        {
            //            graph.AddPointtoLineGraph(item);
            //        }
            //        await Task.Delay(33);
            //    }
            //}

            for (int i = 0; i < 20; i++)
            {
                foreach (var item in ecg_emu)
                {
                    Task.Run(() => SendDataToUpdate(item));
                    await Task.Delay(33);
                }
            }
            for (int i = 0; i < 20; i++)
            {
                foreach (var item in ecg_emu1)
                {
                    Task.Run(() => SendDataToUpdate(item));
                    await Task.Delay(33);
                }
            }
        }

        #region Data Connection

        private async Task KoneksiDenganESP()
        {
            Debug.WriteLine($"The KoneksiDenganESP Thread : {Thread.CurrentThread.Name}");
            List<NetworkInterface> interfaces;
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            tcpClient.ReceiveBufferSize = 50;

            Dispatcher.Invoke(() => ShowCekKompatibilitasWiFi());

            while (!tcpClient.Connected)
            {
                //tcpClient.Connect(IPAddress.Loopback, 12727);
                //await Task.Delay(1000);
                //continue;

                interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(item => item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211).ToList();
                if (interfaces.Count > 0)
                {
                    Dispatcher.Invoke(() => ShowCekKonektivitasAccessPoint());

                    interfaces = interfaces.Where(item => item.OperationalStatus == OperationalStatus.Up).ToList();
                    if (interfaces.Count > 0)
                    {
                        Dispatcher.Invoke(() => ShowCekKonektivitasAlat());
                       
                        foreach (var net_interface in interfaces)
                        {
                            foreach (var gateway_addr in net_interface.GetIPProperties().GatewayAddresses)
                            {
                                if (net_interface.OperationalStatus != OperationalStatus.Up) break;
                                try
                                {
                                    if (tcpClient.ConnectAsync(gateway_addr.Address, 12727).Wait(1000) && tcpClient.Connected)
                                    {
                                        Debug.WriteLine("[TCP CONN]: Terkoneksi ke alat!");
                                        Dispatcher.Invoke(() => ShowTerkoneksiKeAlat());
                                        ReceiveData();
                                        continue;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine($"[TCP CONN EXCEPTION] {e.GetType()} \n {e.StackTrace}");
                                }
                            }
                            if (net_interface.OperationalStatus != OperationalStatus.Up) break;
                        }

                        Debug.WriteLine("[TCP CONN]: Tidak bisa terkoneksi ke Alat!");
                        Dispatcher.Invoke(() => ShowAlatTidakTerkoneksi());
                        await Task.Delay(1000);
                    }
                    else
                    {
                        Debug.WriteLine("[TCP CONN]: Belum terkoneksi ke Access Point Alat!");
                        Dispatcher.Invoke(() => ShowTidakTerkoneksiAccessPoint());
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    Debug.WriteLine("[TCP CONN]: Tidak ada WiFi Adapter, PC ini tidak bisa digunakan untuk koneksi dengan alat!");
                    Dispatcher.Invoke(() => ShowTidakKompatibel());
                    await Task.Delay(1000);
                }
            }

            //Debug.WriteLine("[TCP CONN]: Terkoneksi ke alat!");
            //Dispatcher.Invoke(() => ShowTerkoneksiKeAlat());
            //ReceiveData();
        }

        private void ShowTidakKompatibel()
        {
            check_splash.progres_kompatibilitas.Content = "WiFi tidak tersedia, aplikasi tidak bisa digunakan!";
        }

        private void ShowTidakTerkoneksiAccessPoint()
        {
            check_splash.progres_konek_ap.Content = "Koneksikan ke Access Point Alat!";
        }

        private void ShowAlatTidakTerkoneksi()
        {
            check_splash.progres_konek_alat.Content = "Alat tidak bisa terkoneksi,\npastikan Access Point benar!";
        }

        private void ShowTerkoneksiKeAlat()
        {
            check_splash.progres_konek_alat.Content = "Cek konektivitas Alat OK";
            check_splash.progres_val = 100;
            check_splash.Visibility = Visibility.Hidden;
            rect_splash.Visibility = Visibility.Hidden;
        }

        private void ShowCekKonektivitasAlat()
        {
            check_splash.progres_konek_ap.Content = "Cek konektivitas Access Point OK";
            check_splash.progres_konek_alat.Content = "Cek konektivitas Alat";
            check_splash.progres_val = 200 / 3;
        }

        private void ShowCekKonektivitasAccessPoint()
        {
            check_splash.progres_kompatibilitas.Content = "Cek kompatibilitas WiFi OK";
            check_splash.progres_konek_ap.Content = "Cek konektivitas Access Point";
            check_splash.progres_val = 100 / 3;
        }

        private void ShowCekKompatibilitasWiFi()
        {
            check_splash.progres_kompatibilitas.Content = "Cek kompatibilitas WiFi";
        }

        private async Task ReceiveData()
        {
            Debug.WriteLine($"The ReceiveData Thread : {Thread.CurrentThread.Name}");
            byte[] rxBuf = new byte[100];

            while (tcpClient.Connected)
            {
                try
                {
                    var size = tcpClient.Client.Receive(rxBuf);
                    var data = Encoding.ASCII.GetString(rxBuf, 0, size);
                    Debug.Write($"Received {size} bytes : [ {data}");
                    Debug.WriteLine(" ]");

                    if (size > 0)
                    {
                        Task.Run(() => SendDataToUpdate(data));
                    }
                }
                catch (SocketException sock_err)
                {
                    if (sock_err.SocketErrorCode == SocketError.TimedOut) continue;
                    Debug.WriteLine($"[RECV DATA SOCKET EXCEPTION] {sock_err.SocketErrorCode}");
                    tcpClient.Dispose();
                    Dispatcher.Invoke(() => ResetConnection());
                    KoneksiDenganESP();
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[RECV DATA EXCEPTION] {e.GetType()} \n {e.StackTrace}");
                }
            }
        }

        string last = "";
        private async Task SendDataToUpdate(string data)
        {
            foreach (var item in data.Split('#'))
            {
                var dataCollection = item.Split(',');
                if (dataCollection.Contains(null)) return;

                try
                {
                    switch (dataCollection[0])
                    {
                        case "T":
                            if (dataCollection.Length != 3) break;
                            Debug.WriteLine($"{string.Join(" , ", dataCollection)}");
                            Dispatcher.Invoke(() => UpdateDataView(sysDiaLabel, $"{dataCollection[1]}/{dataCollection[2]}"));
                            break;
                        case "*":
                            if (dataCollection.Length != 8) break;
                            Debug.WriteLine($"{string.Join(" , ", dataCollection)}");
                            if (dataCollection[1] != last) {
                                last = dataCollection[1];
                                if (dataCollection[1] == "1")
                                {
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[0].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V1"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[1].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V2"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[2].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V3"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[3].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V4"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[4].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V5"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[5].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "V6"));
                                }
                                else
                                {
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[0].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG Lead 1"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[1].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG Lead 2"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[2].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG Lead 3"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[3].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG aVR"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[4].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG aVL"));
                                    Dispatcher.Invoke(() => UpdateDataView((ECG_Graphs[5].GetCanvas().Parent as Grid).Children.OfType<Label>().ElementAt(0), "ECG aVF"));
                                }
                            }
                            for (int i = 0; i < 6; i++)
                            {
                                Dispatcher.Invoke(() => UpdateDataView(ECG_Graphs[i], dataCollection[i + 2]));
                            }
                            break;
                        case "N":
                            if (dataCollection.Length != 4) break;
                            Debug.WriteLine($"{string.Join(" , ", dataCollection)}");
                            Dispatcher.Invoke(() => UpdateDataView(Spo2_Graph, dataCollection[1]));
                            Dispatcher.Invoke(() => UpdateDataView(Spo2Label, $"{dataCollection[2]}"));
                            Dispatcher.Invoke(() => UpdateDataView(BPMLabel, $"{dataCollection[3]}"));
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[UPDATE DATA EXCEPTION] {e.GetType()} \n {e.StackTrace}");
                }
            }
        }

        private void ResetConnection()
        {
            check_splash.Reset();
            check_splash.Visibility = Visibility.Visible;
            rect_splash.Visibility = Visibility.Visible;
        }

        #endregion

        #region Update Data

        void UpdateDataView(Label label, string item)
        {
            label.Content = item;
        }

        void UpdateDataView(GraphCanvas graphCanvas, string item)
        {
            graphCanvas.AddPointtoLineGraph(int.Parse(item));
        }

        #endregion
    }
}
