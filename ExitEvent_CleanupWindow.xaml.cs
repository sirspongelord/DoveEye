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
using System.IO;
using DoveVision;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for ExitEvent_CleanupWindow.xaml
    /// </summary>
    public class CleanUPUI
    {
        public int pbMax { get; set; }
        public int pbValue { get; set; }
    }
    public partial class ExitEvent_CleanupWindow : Window
    {
        CleanUPUI userinterface = new CleanUPUI();
        public ExitEvent_CleanupWindow(ref DoveEyeImageCanvas canvas)
        {
            InitializeComponent();
            this.canvas = canvas;
        }
        DoveEyeImageCanvas canvas;
        public bool DeleteComplete = false;


        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            pbCleanUp.Value = 0;
            pbCleanUp.Maximum = canvas.TotalImages;
            foreach (DoveEyeImageGroup group in canvas.ImageGroups)
            {
                foreach (DoveEyeContextualImage image in group.Images)
                {
                    File.Delete(image.Image.bmpFileSource);
                    pbCleanUp.Value++;
                }
            }
            DeleteComplete = true;
        }
        
    }
}
