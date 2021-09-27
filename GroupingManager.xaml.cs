using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using DoveEyeLogic;
using static DoveEyeLogic.DoveEyeImageProcessor;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for GroupingManager.xaml
    /// </summary>
    public class GroupingManagerUserInterface : INotifyPropertyChanged
    {
        //Contains everything the UserInterface needs to Display

        public DoveEyeImageCanvas Canvas
        {
            get { return privateCanvas;}
            set { privateCanvas = value; }
        }//all user interface elements should just be somewhere in there. probably.

        public DoveEyeImageCanvas privateCanvas;

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class GroupingManager : Window
    {

        public DoveEyeImageCanvas canvas;
        public GroupingManagerUserInterface GMUI;
        public GroupingManager(DoveEyeImageCanvas canvas)
        {
            //Grouping Manager displays the results of the analysis to the user. This is incomplete.

            this.canvas = canvas;
            GMUI = new GroupingManagerUserInterface();

            GMUI.privateCanvas = canvas;


            InitializeComponent();
            grid.DataContext = GMUI;

            GMUI.OnPropertyChanged();

            btnQRegroup_Click(null, null);
        }

        private void btnAssignCategory_Click(object sender, RoutedEventArgs e)
        {
            //implement code to assign categories here
            AutoAssignOptions assignWindow = new AutoAssignOptions(ref canvas);
            assignWindow.ShowDialog();

            updateUI();

        }

        private void btnSharpnessSort_Click(object sender, RoutedEventArgs e)
        {
            GMUI.Canvas.SortBySharpness();
            updateUI();
        }

        private void btnIndexSort_Click(object sender, RoutedEventArgs e)
        {
            GMUI.Canvas.SortByIndex();

            updateUI();
        }

        void updateUI()
        {
            grid.DataContext = null;
            grid.DataContext = GMUI;
            GMUI.OnPropertyChanged();
        }

        private void GroupingManagerWindow_Closing(object sender, CancelEventArgs e)
        {
            Environment.Exit(-1);
        }

        private void btnQRegroup_Click(object sender, RoutedEventArgs e)
        {
            QuickRegroupWindow qrWindow = new QuickRegroupWindow(GMUI.Canvas);
            qrWindow.ShowDialog();

            GMUI.Canvas = qrWindow.userinterface.canvas;
            updateUI();
        }

        private void btnFinalize_Click(object sender, RoutedEventArgs e)
        {
            FinalizeWindow finalizeWindow = new FinalizeWindow(GMUI.Canvas);
            finalizeWindow.Show();
        }

        private void btnExposureSort_Click(object sender, RoutedEventArgs e)
        {
            GMUI.Canvas.SortByExposure();

            updateUI();
        }

        private void btnGroupHQAll_Click(object sender, RoutedEventArgs e)
        {
            //Get get group by walking up the tree.
            DoveEyeImageGroup group = (DoveEyeImageGroup)(((ContentPresenter)(((StackPanel)(((StackPanel)(((Button)sender).Parent)).Parent)).TemplatedParent)).Content);

            foreach (DoveEyeContextualImage image in group.Images)
            {
                image.QualitySelectedIndex = 0;
                image.OnPropertyChanged();
            }
            //updateUI();
        }

        private void btnGroupLQAll_Click(object sender, RoutedEventArgs e)
        {
            DoveEyeImageGroup group = (DoveEyeImageGroup)(((ContentPresenter)(((StackPanel)(((StackPanel)(((Button)sender).Parent)).Parent)).TemplatedParent)).Content);

            foreach (DoveEyeContextualImage image in group.Images)
            {
                image.QualitySelectedIndex = 1;
                image.OnPropertyChanged();
            }


            //updateUI();
        }

        private void btnEnlargedView_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Unfortunately, the enlarged view and live preview have not been implemented yet.");
        }
    }
}
