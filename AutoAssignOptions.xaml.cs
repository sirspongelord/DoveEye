using DoveEyeLogic;
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
using Accord.Statistics;

namespace DoveEye
{
    /// <summary>
    /// Interaction logic for AutoAssignOptions.xaml
    /// </summary>
    public class AAOUI : INotifyPropertyChanged
    {
        public double threshold { get; set; }
        public List<string> Variables { get; set; }
        public int VariablesSelectedIndex { get; set; }

        public string LowQualityText { get; set; }
        public string HighQualityText { get; set; }

        public AAOUI()
        {
            Variables = new List<string>();
            Variables.Add("Sharpness");
            Variables.Add("Exposure (Assign Overexposed Images as Low-Quality)");
            Variables.Add("Exposure (Assign Underexposed Images as Low-Quality)");
            Variables.Add("Exposure (Assign BOTH Overexposed AND Underexposed Images as Low-Quality");
            VariablesSelectedIndex = 0;
            //other variables can be added later.
        }

        public enum Variable
        {
            Shaprness,
            ExposureUnderAsLow,
            ExposureOverAsLow,
            ExposureBothAsLow
        }
        public Variable selectedVar { get
            {
                switch(VariablesSelectedIndex)
                {
                    case 0:
                        return Variable.Shaprness;
                        break;
                    case 1:
                        return Variable.ExposureOverAsLow;
                        break;
                    case 2:
                        return Variable.ExposureUnderAsLow;
                        break;
                    case 3:
                        return Variable.ExposureBothAsLow;
                        break;
                    default:
                        throw new Exception();
                }
            } }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
    public partial class AutoAssignOptions : Window
    {
        DoveEyeImageCanvas canvas;
        public AAOUI UserInterface = new AAOUI();
        public AutoAssignOptions(ref DoveEyeImageCanvas canvas)
        {
            this.canvas = canvas;
            InitializeComponent();
            AAGrid.DataContext = UserInterface;
        }

        (double, double) GetExposureThresholds(DoveEyeImageGroup group, double thresholdvalue)
        {
            List<double> ExposureValues = new List<double>();
            foreach (DoveEyeContextualImage image in group.Images)
            {
                ExposureValues.Add(image.Image.Exposure);
            }

            double Mean = Measures.Mean(ExposureValues.ToArray());
            double stDev = Measures.StandardDeviation(ExposureValues.ToArray());
            double SDEVMultiplier = 3 * thresholdvalue;

            if(stDev < 1) { stDev = 1; }

            double upperbound = Mean + SDEVMultiplier * stDev;
            double lowerbound = Mean - SDEVMultiplier * stDev;

            return (lowerbound, upperbound);
        }


        private void btnAssignCategories_Click(object sender, RoutedEventArgs e)
        {
            //this is going to be very similar to code in sldr valuechanged except you're physically categorizing them.
            double thresholdScale = sldrAssignmentCategories.Value;


            foreach (DoveEyeImageGroup group in canvas.ImageGroups)
            {

                (double, double) values = GetExposureThresholds(group, thresholdScale);


                switch (UserInterface.selectedVar)
                {
                    case AAOUI.Variable.Shaprness:
                        group.SortBySharpness();
                        group.ReassignIndices();
                        int HQImages = (int)Math.Ceiling(thresholdScale * (double)group.Images.Count);
                        int LQImages = group.Images.Count - HQImages;
                        for (int i = 0; i < group.Images.Count; i++)
                        {
                            if (i < HQImages) { group.Images[i].QualitySelectedIndex = 0; } else { group.Images[i].QualitySelectedIndex = 1; }
                        }
                        break;
                    case AAOUI.Variable.ExposureUnderAsLow:
                        group.SortByExposure();
                        group.ReassignIndices();
                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if(image.Image.Exposure < values.Item1)
                            {
                                image.QualitySelectedIndex = 1;
                            } else { image.QualitySelectedIndex = 0; }
                        }
                        break;
                    case AAOUI.Variable.ExposureOverAsLow:
                        group.SortByExposure();
                        group.ReassignIndices();
                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if (image.Image.Exposure > values.Item2)
                            {
                                image.QualitySelectedIndex = 1;
                            }
                            else { image.QualitySelectedIndex = 0; }
                        }
                        break;
                    case AAOUI.Variable.ExposureBothAsLow:
                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if (image.Image.Exposure > values.Item2 || image.Image.Exposure < values.Item1)
                            {
                                image.QualitySelectedIndex = 1;
                            }
                            else { image.QualitySelectedIndex = 0; }
                        }
                        break;
                }

            }

            Close();
        }

        private void sldrAssignmentCategories_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //update the two labels that say how many are low vs high category
            int highqualityimages = 0;
            int lowqualityimages = 0;

            double thresholdScale = sldrAssignmentCategories.Value;


            foreach (DoveEyeImageGroup group in canvas.ImageGroups)
            {
                //Consider rewriting this! Perhaps it should autoselect a percentage of all photos
                /**Select photos based on a percentage:
                 * Assign photos as high/low based first on:
                 *  Keeping 1 image per group (use the highest cull value here)
                 *  Then keeping additional images per group that has the most photos?
                 * 
                 * Otherwise, it might be best to display a graph - log of the threshold vs. number of photos that match criteria.
                 * Select the threshold based on the area of the graph with distinctly higher slope?                 * 
                 */
                //lowest and highest for each group. find intermediary threshold. then do comparisons.


                (double, double) exposureThresholds = GetExposureThresholds(group, thresholdScale);

                switch (UserInterface.selectedVar)
                {
                    case AAOUI.Variable.Shaprness:
                        int HQImages = (int)Math.Ceiling(thresholdScale * (double)group.Images.Count);
                        int LQImages = group.Images.Count - HQImages;
                        highqualityimages += HQImages;
                        lowqualityimages += LQImages;
                        break;
                    case AAOUI.Variable.ExposureUnderAsLow:

                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if (image.Image.Exposure < exposureThresholds.Item1)
                            {
                                lowqualityimages++;
                            }
                            else { highqualityimages++; }
                        }

                        break;
                    case AAOUI.Variable.ExposureOverAsLow:


                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if (image.Image.Exposure > exposureThresholds.Item2)
                            {
                                lowqualityimages++;
                            }
                            else { highqualityimages++; }
                        }

                        break;
                    case AAOUI.Variable.ExposureBothAsLow:
                        
                        foreach (DoveEyeContextualImage image in group.Images)
                        {
                            if (image.Image.Exposure < exposureThresholds.Item1 || image.Image.Exposure > exposureThresholds.Item2)
                            {
                                lowqualityimages++;
                            }
                            else { highqualityimages++; }
                        }

                        break;
                }

            }

            UserInterface.HighQualityText = highqualityimages.ToString();
            UserInterface.LowQualityText = lowqualityimages.ToString();
            UserInterface.OnPropertyChanged();
        }
    }
}
