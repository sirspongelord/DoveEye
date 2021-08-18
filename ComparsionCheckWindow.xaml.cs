using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Interop;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for ComparsionCheckWindow.xaml
    /// </summary>
    public partial class ComparsionCheckWindow : Window
    {

        Bitmap Image1;
        Bitmap Image2;

        public bool ComparisonOutcome;
        public bool ComparisonComplete;

        public ComparsionCheckWindow(Bitmap Img1, Bitmap Img2)
        {

            Image1 = Img1;
            Image2 = Img2;

            InitializeComponent();
            img1.Source = Bitmap2BitmapImage(Image1);
            img2.Source = Bitmap2BitmapImage(Image2);

        }
        private BitmapSource Bitmap2BitmapImage(Bitmap bitmap)
        {
            BitmapSource i = Imaging.CreateBitmapSourceFromHBitmap(
                           bitmap.GetHbitmap(),
                           IntPtr.Zero,
                           Int32Rect.Empty,
                           BitmapSizeOptions.FromEmptyOptions());
            return i;
        }
        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            ComparisonOutcome = true;
            ComparisonComplete = true;
            this.Close();
            Image1.Dispose();
            Image2.Dispose();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            ComparisonOutcome = false;
            ComparisonComplete = true;
            this.Close();
            Image1.Dispose();
            Image2.Dispose();
        }
    }
}
