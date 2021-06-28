using LiveCharts;
using LiveCharts.Wpf;
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
        public static double Remap(this double value, double from_min, double from_max, double to_min, double to_max)
        {
            return (value - from_min) / (from_max - from_min) * (to_max - to_min) + to_min;
        }

        public static int Remap(this int value, double from_min, double from_max, double to_min, double to_max)
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

            PointCollection currPoint;
            PointCollection otherPoint;

            public bool Averaging { get; set; } = false;
            public bool Synchronizer { get; set; } = false;
            public bool IsSynchronized { get; set; } = false;
            public int MaxValue { get => graph_max; set => graph_max = value; }
            public int MinValue { get => graph_min; set => graph_min = value; }
            public int MaxStep { get => maxStep; set => maxStep = value; }

            int graph_min = 0, graph_max = 100;
            int point_x = 0, maxStep = 200;
            static int sync_point_x = 0;

            bool loop = false;
            public void AddPointtoLineGraph(double value)
            {
                if (point_x > maxStep)
                {
                    point_x = 0;
                    loop = !loop;

                    if (Averaging)
                    {
                        var avgpx = currPoint.Average(p => p.Y);
                        Debug.WriteLine($"Avg Value : [ {avgpx} ]");
                        var avgval = (int)avgpx.Remap(0, theCanvas.ActualHeight, graph_min, graph_max);
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
                    if (point_x > maxStep - (maxStep / 50)) currPoint.Remove(currPoint.FirstOrDefault());
                    otherPoint.Remove(otherPoint.FirstOrDefault());
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

                var x = point_x.Remap(0, maxStep, 0, theCanvas.ActualWidth);
                var y = value.Remap(graph_min, graph_max, 0, theCanvas.ActualHeight);
                if (IsSynchronized) point_x = sync_point_x;
                else point_x++;
                if (Synchronizer) sync_point_x = point_x;

                Debug.WriteLine($"Value : {value}");
                Debug.WriteLine($"New Point [ {x} , {y} ]");
                currPoint.Add(new Point(x, y));
            }
        }

        GraphCanvas Spo2_Graph;
        GraphCanvas[] ECG_Graphs;

        TcpClient tcpClient;
        Thread th;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            Thread.CurrentThread.Name = "Main UI Thread";
            Debug.WriteLine($"The MainWindow Thread : {Thread.CurrentThread.Name}");

            Spo2_Graph = new(spo2_canvas) { MinValue = 70, MaxValue = 80, MaxStep = 150};

            ECG_Graphs = new[] {
                new GraphCanvas(ecg_1_canvas){ Synchronizer = true },
                new GraphCanvas(ecg_2_canvas){ IsSynchronized = true },
                new GraphCanvas(ecg_3_canvas){ IsSynchronized = true },
                new GraphCanvas(ecg_4_canvas){ IsSynchronized = true },
                new GraphCanvas(ecg_5_canvas){ IsSynchronized = true },
                new GraphCanvas(ecg_6_canvas){ IsSynchronized = true }
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //AddDummyData();
            //th = new Thread(() => KoneksiDenganESP()) { Name = "Connection Thread" };
            //th.Start();
            Task.Run(() => KoneksiDenganESP());
        }

        PointCollection values = new PointCollection();
        private async Task AddDummyData()
        {
            int[] val = new int[] { 102, 117, 131, 132, 123, 114, 105, 96, 88, 87, 102 };
            int[] vel = new int[] { 102, 137, 151, 152, 143, 134, 105, 76, 68, 67, 102 };

            foreach (var item in vel)
            {
                Spo2_Graph.AddPointtoLineGraph(item);
                await Task.Delay(33);
            }

            for (int i = 0; i < 8; i++)
            {
                foreach (var item in val)
                {
                    Spo2_Graph.AddPointtoLineGraph(item);
                    await Task.Delay(33);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                foreach (var item in val)
                {
                    Spo2_Graph.AddPointtoLineGraph(item);
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
                        SendDataToUpdate(data);
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

        string last = "1";
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
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[0].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V1"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[1].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V2"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[2].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V3"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[3].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V4"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[4].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V5"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[5].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "V6"));
                                }
                                else
                                {
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[0].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG Lead 1"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[1].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG Lead 2"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[2].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG Lead 3"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[3].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG aVR"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[4].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG aVL"));
                                    Dispatcher.Invoke(() => UpdateDataView(((Grid)(ECG_Graphs[5].GetCanvas().Parent)).Children.OfType<Label>().ElementAt(0), "ECG aVF"));
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
