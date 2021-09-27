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
using System.Text.RegularExpressions;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    class UI : INotifyPropertyChanged
    {
        //This class doesn't do anything and will eventually be removed.
        public string textforlabel { get; set; }
        public string lblAnalysisText { get; set; }
        public string lblTimeRemainingText { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public int totalimages { get; set; }
        public int progress { get; set; }
        public bool analysiscomplete = false;
        public string tbScalePercentage_Text { get; set; }

        public bool FaceAreaChecked { get; set; }

        public string tbThreadCount_Text { get; set; }
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class MainWindow : Window
    {
        //nothing is implemented in the mainwindow.
        List<DoveEyeContextualImage> Images = new List<DoveEyeContextualImage>();
        UI userinterface = new UI();

        //!! Program will not run unless this filepath exists and contains images !!
        string root = "Y:\\Media\\Image Demo\\";
        
        
        DoveEyeImageCanvas DoveEyeCanvas;
        bool btnStart_Clicked = false;
        
        //DoveEye Main Window Objective: Get image source and similarity analysis settings.
        public string text = "";
        
        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => ExitEventHandler(s,e);
            int totalthreads = Environment.ProcessorCount;
            window = new ExitEvent_CleanupWindow(ref DoveEyeCanvas);
            userinterface.tbThreadCount_Text = (totalthreads - 4).ToString();
            userinterface.tbScalePercentage_Text = 20.ToString();
            Grid.DataContext = userinterface;
        }

        protected void ExitEventHandler(object sender, EventArgs e)
        {
            //Delete all temporary files

            if(DoveEyeCanvas == null)
            {
                return;
            }
            MessageBox.Show("DoveEye will now delete temporary files. Please allow some time for this cleanup process to occur.");

            foreach (DoveEyeImageGroup group in DoveEyeCanvas.ImageGroups)
            {
                foreach (DoveEyeContextualImage image in group.Images)
                {
                    try { File.Delete(image.Image.bmpFileSource); } catch { MessageBox.Show("Error deleting temp file."); }
                }
            }
        }

        

        ExitEvent_CleanupWindow window;
        public void CleanUp()
        {
            window = new ExitEvent_CleanupWindow(ref DoveEyeCanvas);
            window.Show();
        }

        private void btnSelectSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                root = (result == System.Windows.Forms.DialogResult.OK) ? dialog.SelectedPath + "\\" : "";
            }

            //Update Label
            userinterface.textforlabel = "Source: " + root;
            userinterface.OnPropertyChanged();
        }

        void StartAnalysis()
        {
            userinterface.lblAnalysisText = "Verifying Image Files... This may take a short while (<2min).";
            userinterface.OnPropertyChanged();
            DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
            if(root == null || root == "") { MessageBox.Show("Select a folder!"); userinterface.lblAnalysisText = ""; userinterface.OnPropertyChanged(); btnStart_Clicked = false; return; }
            List<DoveEyeImageFileInformation> ImageFileInformation = processor.getImages(root);

            if(ImageFileInformation.Count == 0) { MessageBox.Show("Folder has no photos!"); userinterface.lblAnalysisText = ""; userinterface.OnPropertyChanged(); btnStart_Clicked = false; return; }
            int threads = Convert.ToInt32(userinterface.tbThreadCount_Text);
            int scalepercentage = Convert.ToInt32(userinterface.tbScalePercentage_Text);

            DoveEyeImage.SharpnessAreatype sharpType = userinterface.FaceAreaChecked ? DoveEyeImage.SharpnessAreatype.FacialFallBackAuto : DoveEyeImage.SharpnessAreatype.Auto;
            
            DoveEyeCanvas = new DoveEyeImageCanvas(root, threads, 6, sharpType, ImageFileInformation, scalepercentage);
            DoveEyeCanvas.AnalyzeImages();
            userinterface.lblAnalysisText = "Analyzing Images...";
            DateTime startTime = DateTime.Now;

            while (!DoveEyeCanvas.AnalysisManager.AnalysisComplete)
            {
                Thread.Sleep(100);
                userinterface.totalimages = DoveEyeCanvas.AnalysisManager.TotalFiles;
                userinterface.progress = DoveEyeCanvas.AnalysisManager.AnalysisProgress;
                //calculate time remaining
                TimeSpan elaspedTime = DateTime.Now.Subtract(startTime);
                double secondsRemaining = ((double)DoveEyeCanvas.AnalysisManager.AnalysisProgress / elaspedTime.TotalSeconds) * (DoveEyeCanvas.AnalysisManager.TotalFiles - DoveEyeCanvas.AnalysisManager.AnalysisProgress);
                userinterface.lblTimeRemainingText = "Time Remaining: " + Math.Floor(secondsRemaining);
                userinterface.OnPropertyChanged();
            }

            userinterface.lblAnalysisText = "Analyzing Image Groupings...";

            DoveEyeCanvas.AnalyzeGroupings();

            while (!DoveEyeCanvas.GroupingManager.AnalysisComplete)
            {
                userinterface.progress = DoveEyeCanvas.GroupingManager.Progress + 1;
                userinterface.OnPropertyChanged();
                Thread.Sleep(100);
            }

            DoveEyeCanvas.ImageGroups = DoveEyeCanvas.GroupingManager.Groupings;

            DoveEyeCanvas.AssignIndices();



            GroupingManager groupmanager = new GroupingManager(DoveEyeCanvas);

            groupmanager.Show();

            System.Windows.Threading.Dispatcher.Run();
            //need to find out how to close this thread.
        }
        private void btnStartAnalysis_Click(object sender, RoutedEventArgs e)
        {
            //This is incomplete.

            //Once the analysis starts, the program assigns DoveEyeCanvas to a new instance of the DoveEyeImageCanvas class.
            //Then, it begins analyzing the images. Once done, it analyzes the groupings. 

            //No user feedback is given. In fact, the thread is slept while the analysis managers are analyzing their images. 
            //This should be replaced with a new window that shows a progress bar that is dynamically updated based on the analysis progress, along with
            //  a cancel button.
            if(btnStart_Clicked) { return; }
            btnStart_Clicked = true;

            Thread AnalysisStartThread = new Thread(new ThreadStart(StartAnalysis));
            AnalysisStartThread.SetApartmentState(ApartmentState.STA);
            AnalysisStartThread.Start();


            //progress bar stuff goes here in a new thread potentially.

            //wait until completed and then close?? how do i do this, this thread is the one that needs to close...
        }

        private void tbThreadCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+"); //woah fancy colors
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(-1);
        }
    }
}
