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
        public DoveEyeImageCanvas Canvas; //all user interface elements should just be somewhere in there. probably.

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class GroupingManager : Window
    {
        public DoveEyeImageCanvas canvas;
        public GroupingManager(DoveEyeImageCanvas canvas)
        {
            InitializeComponent();
            this.canvas = canvas;
            lvContent.DataContext = canvas.ImageGroups;
        }
    }
}
