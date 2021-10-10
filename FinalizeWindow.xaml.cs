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
using System.Windows.Shapes;
using System.Threading;
using DoveVision;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for FinalizeWindow.xaml
    /// </summary>
    public class FinalizeWindowUI : INotifyPropertyChanged
    {
        public string SourceText
        {
            get
            {
                return "Source: " + Source;
            }
        }
        public string DestinationText { get { return "Destination: " + Destination; } }

        public string AnalysisLabel_Text { get; set; }
        public string RichBox_Text { get; set; }

        public int pbValue { get; set; }
        public int pbMaximum { get; set; }



        public string Source { get; set; }
        public string Destination { get; set; }

        public bool DeletePhotos { get { return privDelPhotos; } set {
                displayWarning = value ? Visibility.Visible : Visibility.Hidden; OnPropertyChanged(); privDelPhotos = value; } }
        private bool privDelPhotos = false;
        public bool FolderGroups { get; set; }

        public Visibility displayWarning { get; private set; }

        public bool KeepSrcAsDest { 
            get 
            {
                return privKeepSrcAsDest;
            } 
            set 
            {
                switch(value)
                {
                    case true:
                        privKeepSrcAsDest = value;
                        Destination = Source;
                        OnPropertyChanged();
                        break;
                    case false:
                        //shouldn't need to do anything?
                        privKeepSrcAsDest = value;
                        break;
                }
            } 
        }
        bool privKeepSrcAsDest;



        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class FinalizeWindow : Window
    {
        DoveEyeImageCanvas canvas;
        public FinalizeWindowUI userinterface;
        public FinalizeWindow(DoveEyeImageCanvas canvas)
        {
            this.canvas = canvas;
            userinterface = new FinalizeWindowUI();
            InitializeComponent();

            FinalizeGrid.DataContext = userinterface;
            userinterface.Source = canvas.root;
            userinterface.pbMaximum = canvas.TotalImages;
            userinterface.pbValue = 0;
            userinterface.DeletePhotos = false;
            userinterface.OnPropertyChanged();
        }


        private void btnSelectDest_Click(object sender, RoutedEventArgs e)
        {
            string Destination;

            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                Destination = (result == System.Windows.Forms.DialogResult.OK) ? dialog.SelectedPath + "\\" : "";
            }

            userinterface.Destination = Destination;
            userinterface.OnPropertyChanged();
        }

        public bool SortingComplete = false;
        private void SortPhotos()
        {
            userinterface.AnalysisLabel_Text = "Moving Photos...";
            if (userinterface.FolderGroups)
            {

                //make folders for each group

                foreach (DoveEyeImageGroup group in canvas.ImageGroups)
                {
                    //create low and high quality dest
                    group.GroupName = Regex.Replace(group.GroupName, "[^a-zA-Z0-9_]+", "_", RegexOptions.Compiled); //protect against invalid folder names
                    string LowQualityDest = userinterface.Destination + group.GroupName + "\\Low Quality\\";
                    string HighQualityDest = userinterface.Destination + group.GroupName + "\\High Quality\\";

                    Directory.CreateDirectory(LowQualityDest);
                    Directory.CreateDirectory(HighQualityDest);

                    foreach (DoveEyeContextualImage image in group.Images)
                    {
                        switch (image.QualityStates[image.QualitySelectedIndex])
                        {
                            case "Low Quality":
                                foreach (string extension in image.Image.fileinformation.Extensions)
                                {
                                    //construct destination file path.
                                    string source = image.Image.fileinformation.FilePath + image.Image.fileinformation.FileName + extension;
                                    string destination = LowQualityDest + image.Image.fileinformation.FileName + extension;

                                    if (userinterface.DeletePhotos)
                                    {
                                        userinterface.RichBox_Text += string.Format("Deleting {0} \n", source);
                                        MessageBoxResult result = MessageBox.Show("Warning! About to delete photo " + source, "", MessageBoxButton.OKCancel);
                                        if (result == MessageBoxResult.OK)
                                        {
                                            try { File.Delete(source); } catch { MessageBox.Show("Error occurred while deleting " + source); }
                                        }
                                    }
                                    else
                                    {
                                        userinterface.RichBox_Text += string.Format("Moving {0} to {1} \n", source, destination);
                                        try { File.Move(source, destination); }
                                        catch { MessageBox.Show("An error occurred while moving file " + source + " to " + destination); }
                                    }
                                }
                                //Need to figure out how my backend handled multiple image file extensions.
                                break;
                            case "High Quality":
                                foreach (string extension in image.Image.fileinformation.Extensions)
                                {
                                    //construct destination file path.
                                    string source = image.Image.fileinformation.FilePath + image.Image.fileinformation.FileName + extension;
                                    string destination = HighQualityDest + image.Image.fileinformation.FileName + extension;
                                    userinterface.RichBox_Text += string.Format("Moving {0} to {1} \n", source, destination);

                                    try { File.Move(source, destination); }
                                    catch
                                    {
                                        MessageBox.Show("An error occurred while moving file " + source + " to " + destination);
                                    }

                                }
                                break;
                        }
                        userinterface.pbValue++;
                        userinterface.OnPropertyChanged();
                    }
                }
            }
            else
            {
                //just make two big folders.
                string HighQualityDest = userinterface.Destination + "High Quality\\";
                string LowQualityDest = userinterface.Destination + "Low Quality\\";
                Directory.CreateDirectory(LowQualityDest);
                Directory.CreateDirectory(HighQualityDest);

                //now start moving all the photos.
                foreach (DoveEyeImageGroup group in canvas.ImageGroups)
                {
                    foreach (DoveEyeContextualImage image in group.Images)
                    {
                        switch (image.QualityStates[image.QualitySelectedIndex])
                        {
                            case "Low Quality":
                                foreach (string extension in image.Image.fileinformation.Extensions)
                                {
                                    //construct destination file path.
                                    string source = image.Image.fileinformation.FilePath + image.Image.fileinformation.FileName + extension;
                                    string destination = LowQualityDest + image.Image.fileinformation.FileName + extension;
                                    
                                    if (userinterface.DeletePhotos)
                                    {
                                        userinterface.RichBox_Text += string.Format("Deleting {0} \n", source);

                                        MessageBoxResult result = MessageBox.Show("Warning! About to delete photo " + source, "", MessageBoxButton.OKCancel);
                                        if (result == MessageBoxResult.OK)
                                        {
                                            try { File.Delete(source); } catch { MessageBox.Show("Error occurred while deleting " + source); }
                                        }
                                    }
                                    else
                                    {
                                        userinterface.RichBox_Text += string.Format("Moving {0} to {1} \n", source, destination);

                                        try { File.Move(source, destination); }
                                        catch { MessageBox.Show("An error occurred while moving file " + source + " to " + destination); }
                                    }
                                }
                                //Need to figure out how my backend handled multiple image file extensions.
                                break;
                            case "High Quality":
                                foreach (string extension in image.Image.fileinformation.Extensions)
                                {
                                    //construct destination file path.
                                    string source = image.Image.fileinformation.FilePath + image.Image.fileinformation.FileName + extension;
                                    string destination = HighQualityDest + image.Image.fileinformation.FileName + extension;
                                    userinterface.RichBox_Text += string.Format("Moving {0} to {1} \n", source, destination);

                                    try { File.Move(source, destination); }
                                    catch
                                    {
                                        MessageBox.Show("An error occurred while moving file " + source + " to " + destination);
                                    }

                                }
                                break;
                        }
                        userinterface.pbValue++;
                        userinterface.OnPropertyChanged();
                    }
                }
            }
            SortingComplete = true;
            MessageBox.Show("Images successfully sorted. Program will now close. Please fill the 30-second feedback form to help improve this software");
            
            System.Diagnostics.Process.Start("http://feedback.dove.vision");

            MessageBox.Show("DoveEye will now delete temporary files. Please allow some time for this cleanup process to occur.");

            foreach (DoveEyeImageGroup group in canvas.ImageGroups)
            {
                foreach (DoveEyeContextualImage image in group.Images)
                {
                    try { File.Delete(image.Image.bmpFileSource); } catch { MessageBox.Show("Error deleting temp file."); }
                }
            }

            Environment.Exit(-1);
        }

        private void btnSortPhotos_Click(object sender, RoutedEventArgs e)
        {
            //execute all sorts.
            //check if you should create folders per group or just make two big folders
            if (userinterface.Destination == null || userinterface.Destination == "") { MessageBox.Show("Select a Destination"); return; }

            Thread sortThread = new Thread(new ThreadStart(SortPhotos));
            sortThread.Start();
        }

        private void btnFeedback_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://feedback.dove.vision");
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://donate.dove.vision");
        }
    }
}
