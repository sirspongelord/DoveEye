using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.IO;
using System.Timers;
using System.Threading;
using DoveEyeLogic;
using ImageMagick;
using System.ComponentModel;
using static DoveEyeLogic.DoveEyeImageProcessor;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    class UI : INotifyPropertyChanged
    {
        public string textforlabel = "";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class MainWindow : Window
    {
        List<DoveEyeContextualImage> Images = new List<DoveEyeContextualImage>();
        UI userinterface = new UI();
        DoveEyeImageCanvas DoveEyeCanvas = new DoveEyeImageCanvas("Y:\\Media\\Image Demo\\", 8, 6);

        string root = "Y:\\Media\\Image Demo\\";
        
        //DoveEye Main Window Objective: Get image source and similarity analysis settings.
        public string text = "";
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnSelectSource_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void btnStartAnalysis_Click(object sender, RoutedEventArgs e)
        {
            DoveEyeCanvas.AnalyzeImages();

            while(!DoveEyeCanvas.AnalysisManager.AnalysisComplete)
            {
                Thread.Sleep(100);
            }

            DoveEyeCanvas.AnalyzeGroupings();

            while(!DoveEyeCanvas.manager.AnalysisComplete)
            {
                Thread.Sleep(100);
            }

            GroupingManager groupmanager = new GroupingManager(DoveEyeCanvas);

            groupmanager.Show();
        }
    }
}
