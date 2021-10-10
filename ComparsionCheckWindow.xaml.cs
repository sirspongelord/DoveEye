using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using DoveVision;
using System.Windows.Interop;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using static DoveVision.DoveEyeImageProcessor.DoveEyeGroupingManager;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for ComparsionCheckWindow.xaml
    /// </summary>
    class ComparisonCheckUI : INotifyPropertyChanged
    {
        public DoveEyeImage image1 { get; set; }
        public DoveEyeImage image2 { get; set; }

        public double comparisonValue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class ComparsionCheckWindow : Window
    {

        public List<ImagePairComparisonState> images;
        int Imageindex = 0;

        ComparisonCheckUI userinterface = new ComparisonCheckUI();

        public ComparsionCheckWindow(ref List<ImagePairComparisonState> ComparisonImages)
        {
            InitializeComponent();

            
            //TODO ref a progress to show a progress bar.
            //The comparison check window prompts the user to ask if two images belong in the same group. This is called if the analysis is within a certain
            //intermediary threshold.
            images = ComparisonImages;
            updateUI();
            btnYes_Click(null, null);
            //Find the first image and determine it to be active 

            //TODO: Databind a new class that contains userinterface information for this one with propretychanged info.
        }

        bool PromptInProgress = false;
        public bool AllImagesPrompted = false;
        void updateUI()
        {
            CCGrid.DataContext = null;
            CCGrid.DataContext = userinterface;
        }
        public void PromptNext()
        {
            //Fix this code. seems to throw stackoverflow exceptions sometimes - not sure why?
            Imageindex++;
            if (!PromptInProgress)
            {
                //must move this if statement to the end. move ++ to the end as well.
                if (Imageindex == images.Count)
                {
                    //reached the end. Go back to the beginning and start to check again.
                    Imageindex = 0;

                    //check if an escape should be done? or let the other part handle it?
                    bool Complete = true;
                    foreach (ImagePairComparisonState state in images)
                    {
                        if (state.State == ImagePairComparisonState.ComparisonState.PendingAnalysis || state.State == ImagePairComparisonState.ComparisonState.AnalysisInProgress || state.State == ImagePairComparisonState.ComparisonState.PendingUserRequest)
                        {
                            //something isn't done yet.

                            Complete = false;
                            break;
                        }
                    }
                    if (Complete)
                    {
                        //all human prompts are answered. close the window.
                        AllImagesPrompted = true;
                        this.Close();
                    }
                    else
                    {
                        //not done yet. sleep the thread for a bit and check again
                        Thread.Sleep(200);
                        PromptNext();
                    }

                }
                else if (images[Imageindex].State == ImagePairComparisonState.ComparisonState.PendingUserRequest)
                {
                    //prompt the images
                    userinterface.image1 = images[Imageindex].image1.Image;
                    userinterface.image2 = images[Imageindex].image2.Image;
                    userinterface.comparisonValue = images[Imageindex].result.ComparisonValue;
                    //set promptinprogress to true
                    updateUI();
                    PromptInProgress = true;
                    //when the user selects yes or no, the state will be updated accordingly and this funciton will be called to prompt next.

                }
                else
                {
                    Thread.Sleep(30); //potentially solves stackoverflow exceptions
                    PromptNext();
                }
            }
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            //set current image state and prompt next.
            if(!AllImagesPrompted)
            {
                images[Imageindex].State = ImagePairComparisonState.ComparisonState.Similar;
                PromptInProgress = false;
                PromptNext();
            }
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            if (!AllImagesPrompted)
            {
                images[Imageindex].State = ImagePairComparisonState.ComparisonState.NotSimilar;
                PromptInProgress = false;
                PromptNext();
            }
        }
    }
}
