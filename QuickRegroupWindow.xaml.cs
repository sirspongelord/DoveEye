using DoveVision;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for QuickRegroupWindow.xaml
    /// </summary>
    public class Minus30Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            return ((double)value) - 30;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class Minus400Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            return ((double)value) - 400;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class QGUI : INotifyPropertyChanged
    {
        public DoveEyeImageCanvas canvas { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class QuickRegroupWindow : Window
    {
        public QGUI userinterface = new QGUI();
        public QuickRegroupWindow(DoveEyeImageCanvas canvas)
        {
            userinterface.canvas = canvas;
            InitializeComponent();
            qgGrid.DataContext = userinterface;
        }

        private void btnFinish_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        void UpdateUI()
        {


            //reset user interface
            //qgGrid.DataContext = null;
            //qgGrid.DataContext = userinterface;

            //Why do you need to do this? Because you do. List<DoveEyeImageGroup> is not an observable collection because I didn't make it one
        }

        private void MrgUp_Click(object sender, RoutedEventArgs e)
        {
            int selectionIndex = getButtonGroup(sender);

            if(selectionIndex != 0)
            {
                userinterface.canvas.MergeGroups(selectionIndex, selectionIndex - 1);
            }

            UpdateUI();
        }
        private int getButtonGroup(object btnsender)
        {
            //sender is a button
            //sender parent is a stackpanel
            //sender parent parent should be another stackpanel
            //sender parent parent parent should be a doveeyeimagegroup
            DoveEyeImageGroup group = (DoveEyeImageGroup)((ContentPresenter)((StackPanel)((StackPanel)((Button)btnsender).Parent).Parent).TemplatedParent).Content;

            //find group in user interface
            int groupID = 0;
            for (int i = 0; i < userinterface.canvas.ImageGroups.Count; i++)
            {
                if (userinterface.canvas.ImageGroups[i] == group)
                {
                    groupID = i;
                }
            }

            return groupID;
        }

        private int splitSelectionIndex;
        private bool splittingInProgress = false;
        private object btnSender;
        private void Split_Click(object sender, RoutedEventArgs e)
        {
            int selectionIndex = getButtonGroup(sender);
            ((Button)sender).Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            splitSelectionIndex = selectionIndex;
            splittingInProgress = true;
            btnSender = sender;
            //Waiting for selectionchanged event handler.
        }

        private void MrgDown_Click(object sender, RoutedEventArgs e)
        {
            int selectionIndex = getButtonGroup(sender);

            if (selectionIndex != userinterface.canvas.ImageGroups.Count-1)
            {
                userinterface.canvas.MergeGroups(selectionIndex, selectionIndex + 1);
            }

            UpdateUI();
        }


        private void lbImgGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //get parent
            int selection = lvGroupContent.Items.IndexOf(((ContentPresenter)((StackPanel)(((ListBox)sender)).Parent).TemplatedParent).Content);
            if (selection != -1)
            {
                DoveEyeImageGroup group = userinterface.canvas.ImageGroups[selection];
                //find group in user interface
                int groupID = 0;
                for (int i = 0; i < userinterface.canvas.ImageGroups.Count; i++)
                {
                    if (userinterface.canvas.ImageGroups[i] == group)
                    {
                        groupID = i;
                    }
                }

                //find selection
                int itemID = ((ListBox)sender).SelectedIndex;

                //check if splitting is in progress.
                if (splittingInProgress)
                {
                    //split group
                    userinterface.canvas.SplitGroup(groupID, itemID);

                    splittingInProgress = false;
                    ((Button)btnSender).Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));

                    UpdateUI();
                }
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://help.dove.vision");
        }
    }
}
