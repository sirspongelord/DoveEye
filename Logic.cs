using System;
using System.Drawing;
using Accord.Imaging;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.Generic;
using Accord.Imaging.Filters;
using System.Threading;
using Accord.Statistics;
using ImageMagick;
using System.Linq;
using DoveEyeLogic;
using System.Threading.Tasks;
using static DoveEyeLogic.DoveEyeImageProcessor;
using DoveEye;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows;

namespace DoveEyeLogic
{
    //all logic/backend for DoveEye
    public class DoveEyeContextualImage
    {
        //Includes a DoveEyeImage with relevant contextual properties set by the user.

        public DoveEyeImage Image;

        public int ImageIndex;
        public int ImageGroup;
        public FileInfo ImageInfo;

        public ImageArea SharpnessArea
        {
            get
            {
                if (PrivateSharpnessArea.Equals(default(ImageArea)))
                {
                    SmartAutoAssignSharpnessArea();
                }
                return PrivateSharpnessArea;
            }
            set
            {
                PrivateSharpnessArea = value;
            }
        }//Sharpness Area. Default is the "Smart" area, though can be overwritten as center.
        private ImageArea PrivateSharpnessArea;

        public double Sharpness
        {
            get
            {
                return PrivateSharpness;
            }
        }
        private double PrivateSharpness = -1;

        public enum ExtensionStates : int
        {
            Best,
            Rest,
            Delete
        }

        public class ImageState
        {
            public string ExtentionValue;
            public ExtensionStates ExtensionState;
            public bool CheckBox
            {
                get { return ExtensionState == ExtensionStates.Best; }
            }
        }

        public List<ImageState> ImageStates;


        public DoveEyeContextualImage(DoveEyeImageFileInformation fileInfo, FileStream stream)
        {
            ResourceLimits.LimitMemory(new Percentage(1.00));
            string FilePath = fileInfo.FilePath + fileInfo.FileName;
            List<string> extensions = fileInfo.Extensions;

            Image = new DoveEyeImage(FilePath, 20, extensions, stream, fileInfo.BestFileInfo);

            //assign delete images
            ImageStates = new List<ImageState>();
            foreach (string ext in Image.FileExtensions)
            {
                ImageState state = new ImageState();
                state.ExtensionState = ExtensionStates.Best;
                state.ExtentionValue = ext;
                ImageStates.Add(state);
            }
        }

        public void SmartAutoAssignSharpnessArea()
        {
            //finds average x and y feature point of the image and creates assigns ImageArea to a rectangle surrounding 1 standard deviation of the average.
            double sDevScale = 1.0;

            List<double> XCoords = new List<double>();
            List<double> YCoords = new List<double>();

            //Store X and Y into a new array
            foreach (SpeededUpRobustFeaturePoint point in Image.ImageFeatures)
            {
                XCoords.Add(point.X);
                YCoords.Add(Image.height - point.Y);
            }

            int meanX = (int)Math.Floor(Measures.Mean(XCoords.ToArray()));
            int SDevX = (int)Math.Floor(Measures.StandardDeviation(XCoords.ToArray()));
            int meanY = (int)Math.Floor(Measures.Mean(YCoords.ToArray()));
            int SDevY = (int)Math.Floor(Measures.StandardDeviation(YCoords.ToArray()));

            //create image area
            ImageArea area = new ImageArea((int)(meanX - sDevScale * (SDevX)), (int)(meanY - sDevScale * (SDevY)), (int)(sDevScale * SDevX * 2), (int)(sDevScale * SDevY * 2));
            PrivateSharpnessArea = area;
            //Use Feature Points to get the most probably sharpness area range.
        }

        public void ComputeSharpness()
        {
            DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
            PrivateSharpness = processor.GetSharpness(SharpnessArea, Image.FilePath);
        }


        public struct ImageArea
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public ImageArea(int X, int Y, int Width, int Height)
            {
                this.X = X;
                this.Y = Y;
                this.Width = Width;
                this.Height = Height;
            }
        }
    }

    public class DoveEyeImage
    {
        //Includes only objective information about the image. No User-Defined Context exists.
        public string FileName; //Name of File
        public string FilePath;
        public Bitmap Thumbnail;
        public BitmapSource DisplayThumbnail
        {
            get
            {
                //takes around 10ms. This could slow down user interface performance, but it also may not be too bad
                BitmapSource i = Imaging.CreateBitmapSourceFromHBitmap(
                              Thumbnail.GetHbitmap(),
                              IntPtr.Zero,
                              Int32Rect.Empty,
                              BitmapSizeOptions.FromEmptyOptions());
                return i;
            }
        }
        public DoveEyeHistogram Histogram;

        public string Aperture;
        public string ISOSpeed;
        public string ShutterSpeed;
        public DateTime dateTaken;

        public bool FileUsageComplete = false;
        public bool AnalysisComplete = false;

        public double Exposure;

        public int width;
        public int height;

        public List<string> FileExtensions;

        private double ProcessingScale; //for thumbnail and feature point size

        //Calculated upon read unless already calculated.
        public List<SpeededUpRobustFeaturePoint> ImageFeatures
        {
            get
            {
                if (!FeaturesAnalyzed)
                {
                    DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                    PrivateImageFeatures = processor.GetFeatures(Thumbnail, ProcessingScale);
                }
                return PrivateImageFeatures;
            }
        }
        bool FeaturesAnalyzed
        {
            get { return PrivateImageFeatures != null; }
        }
        private List<SpeededUpRobustFeaturePoint> PrivateImageFeatures;
        void ImageCreationThread()
        {
            file.Position = 0;
            GC.Collect(); //this is not the best way to do this
            MagickImage tempMagickImage = new MagickImage(file);
            FileUsageComplete = true;
            file.Dispose();

            //store original image width/height
            width = tempMagickImage.Width;
            height = tempMagickImage.Height;

            //EXIF reading does not work correctly.
            //IExifProfile exifProfile = tempMagickImage.GetExifProfile();
            //IExifValue DateTimeOriginalTaken = exifProfile.GetValue(ExifTag.DateTimeOriginal);
            //IExifValue ExifAperture = exifProfile.GetValue(ExifTag.ApertureValue);
            //IExifValue ExifISO = exifProfile.GetValue(ExifTag.ISOSpeed);
            //IExifValue ExifShutterSpeed = exifProfile.GetValue(ExifTag.ShutterSpeedValue);

            //Resize Image, Store Thumbnail
            tempMagickImage.Resize(new Percentage(ProcessingScale));
            using (MemoryStream memstream = new MemoryStream())
            {
                tempMagickImage.Write(memstream, MagickFormat.Jpg);
                Thumbnail = new Bitmap(memstream);
                memstream.Dispose();
            }
            //DisplayThumbnail = BitmapToImageSource(Thumbnail);


            //histogram from thumbnail (can be improved later)
            ImageStatistics imageStatistics = new ImageStatistics(Thumbnail);
            Histogram = new DoveEyeHistogram(imageStatistics);

            //EXIF READING DOES NOT WORK!!

            //EXIF READING DOES NOT WORK
            Aperture = "Not Implemented";
            //ISOSpeed = ExifISO.ToString();
            //ShutterSpeed = ExifShutterSpeed.ToString();
            //dateTaken = DateTime.Parse(DateTimeOriginalTaken.ToString()); //!! Likely to fail !!

            //Dispose Image
            tempMagickImage.Dispose();


            //Analyze the features
            DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
            PrivateImageFeatures = processor.GetFeatures(Thumbnail, ProcessingScale);

            //exposure
            Exposure = (Histogram.Red.Mean + Histogram.Blue.Mean + Histogram.Green.Mean) / 3;
            AnalysisComplete = true;

        }

        FileStream file;
        public DoveEyeImage(string filepath, double ScalePercentageHighThumbnail, List<string> extensions, FileStream stream, FileInfo info)
        {
            ResourceLimits.LimitMemory(new Percentage(1.00));
            try
            {
                Thread.Sleep(500);
                file = stream;

                ProcessingScale = ScalePercentageHighThumbnail;
                //Put this in a thread to avoid stopping the source thread. Requires further management.
                Thread processingThread = new Thread(new ThreadStart(ImageCreationThread));
                processingThread.Start();

                dateTaken = info.CreationTime;

                //Get file extensions
                FileExtensions = extensions;
                FileName = info.Name;
                FilePath = info.FullName;
            }
            catch
            {
                throw new NotImplementedException();
            }
        }

        public class DoveEyeHistogram
        {
            public Accord.Statistics.Visualizations.Histogram Red;
            public Accord.Statistics.Visualizations.Histogram Green;
            public Accord.Statistics.Visualizations.Histogram Blue;

            ImageStatistics ImageStatistics; //in case it is needed in the future.
            public DoveEyeHistogram(Accord.Imaging.ImageStatistics imageStatistics)
            {
                Red = imageStatistics.Red;
                Green = imageStatistics.Green;
                Blue = imageStatistics.Blue;

                ImageStatistics = imageStatistics;
            }

            public Bitmap GetBitmap()
            {
                throw new NotImplementedException();
            }
        }
    }


    public enum ImageComparisonType : int
    {
        Basic,
        Detailed
        //FeaturePoint,
        //FeaturePointVectors,
        //DateTimeDifference,
        //ColorSimilarity
    }
    public struct DoveEyeFeatureVector
    {
        DoveEyeFeaturePoint Image1Point;
        DoveEyeFeaturePoint Image2Point;

        readonly public double PolarDistance;
        readonly public double PolarTheta;
        readonly public double RectangularX;
        readonly public double RectangularY;

        public DoveEyeFeatureVector(int Image1X, int Image1Y, int Image2X, int Image2Y)
        {
            Image1Point.X = Image1X;
            Image1Point.Y = Image1Y;
            Image2Point.X = Image2X;
            Image2Point.Y = Image2Y;

            RectangularX = Image1X - Image2X;
            RectangularY = Image1Y - Image2Y;

            PolarDistance = Math.Sqrt(Math.Pow(RectangularX, 2) + Math.Pow(RectangularY, 2));
            PolarTheta = Math.Atan2(RectangularX, RectangularY);
        }
    }
    public struct DoveEyeFeaturePoint
    {
        public double X;
        public double Y;
        public DoveEyeFeaturePoint(double X, double Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
    public struct DoveEyeImageComparisionResult
    {
        public ImageComparisonType ImageComparisonType;
        public double ComparisonValue;
        public bool Similar;


        public List<SpeededUpRobustFeaturePoint> Image1Features;
        public List<SpeededUpRobustFeaturePoint> Image2Features;

        public List<DoveEyeFeatureVector> FeatureVectors;

        public DoveEyeImageComparisionResult(ImageComparisonType ComparisonType, DoveEyeImage Image1, DoveEyeImage Image2, bool Similar, double comparisonValue)
        {
            ImageComparisonType = ComparisonType;
            Image1Features = Image1.ImageFeatures;
            Image2Features = Image2.ImageFeatures;

            this.Similar = Similar;
            ComparisonValue = comparisonValue;

            FeatureVectors = new List<DoveEyeFeatureVector>(); //set to a non-empty list if relevant.
        }

    }


    public class DoveEyeImageFileInformation
    {
        public string FileName;
        public string FilePath;
        public List<string> Extensions;
        public string FullFilePathPreferred;

        public int Index;
        public string NextImagePath;

        public FileInfo BestFileInfo;

        public DoveEyeImageFileInformation(string sourcedirectory, string filename, List<string> extensions, int index)
        {
            Index = index;
            FileName = filename;
            FilePath = sourcedirectory + FilePath;
            Extensions = extensions;
            FullFilePathPreferred = FilePath + filename + extensions[0];
            BestFileInfo = new FileInfo(FullFilePathPreferred);
        }
    }


    public class DoveEyeImageGroup
    {
        //information in a specific grouping
        public List<DoveEyeContextualImage> Images;

        public int GroupIndex;
        public string GroupName;

        public int MaxSharpness;
        public int MinSharpness;


        public void SortBySharpness()
        {
            throw new NotImplementedException();
        }

        public void SortByIndex()
        {
            throw new NotImplementedException();
        }
    }

    public class DoveEyeImageCanvas
    {
        //name can be do better
        public string root;
        public List<DoveEyeImageFileInformation> ImageFiles;
        public int TotalImages;

        int threads;
        int buffer;

        public DoveEyeGroupingManager manager;

        public DoveEyeImageCanvas(string root, int threads, int buffer)
        {
            this.root = root;
            this.threads = threads;
            this.buffer = buffer;
        }

        public List<DoveEyeImageGroup> ImageGroups;

        public DoveEyeAnalysisManager AnalysisManager;

        public void MergeGroups(int index1, int index2)
        {
            DoveEyeImageGroup MergedGroups = new DoveEyeImageGroup();
            ImageGroups[index1].Images.AddRange(ImageGroups[index2].Images);
            MergedGroups.Images = ImageGroups[index1].Images;
            MergedGroups.GroupName = ImageGroups[index1].GroupName;
            MergedGroups.GroupIndex = index1;
            MergedGroups.SortByIndex();

            ImageGroups.Remove(ImageGroups[index1]);
            ImageGroups.Remove(ImageGroups[index2]);
            ImageGroups.Insert(index1, MergedGroups);
            ConsolidateIndices();
        }
        void ConsolidateIndices()
        {
            for(int i = 0; i < ImageGroups.Count; i++)
            {
                ImageGroups[i].GroupIndex = i;
            }
        }

        public void SplitGroup(int GroupIndex, int ImageIndex)
        {
            throw new NotImplementedException();
        }


        public void AnalyzeImages()
        {
            AnalysisManager = new DoveEyeAnalysisManager(root, threads, buffer);
            AnalysisManager.BeginAnalysis();
        }

        public void AnalyzeSharpness()
        {
            throw new NotImplementedException();
        }

        public void AnalyzeGroupings()
        {
            manager = new DoveEyeGroupingManager(ImageComparisonType.Detailed,AnalysisManager.Images);
            manager.BeginGroupAnalysis();
        }
    }


    public class DoveEyeImageProcessor
    {
        //Functions for Image Comparison

        public List<SpeededUpRobustFeaturePoint> GetFeatures(Bitmap Image, double scale)
        {
            //scale required to map the feature points directly to the source image. scale can be varied and is from 0 to 1.
            Grayscale grayscale = new Grayscale(0.3333, 0.3333, 0.3333);

            grayscale.Apply(Image);

            SpeededUpRobustFeaturesDetector detector = new SpeededUpRobustFeaturesDetector();
            List<SpeededUpRobustFeaturePoint> Features = detector.ProcessImage(Image);

            return Features;
        }
        public DoveEyeImageComparisionResult CompareImages(DoveEyeImage Image1, DoveEyeImage Image2, ImageComparisonType comparisonType)
        {
            //implement comparison here by switching on ImageComparisonType
            switch (comparisonType)
            {
                case ImageComparisonType.Basic:
                    DoveEyeImageComparisionResult BasicComparisonResult = BasicComparison(Image1, Image2); //create methods for each
                    return BasicComparisonResult;
                case ImageComparisonType.Detailed:
                    DoveEyeImageComparisionResult DetailedComparisonResult = DetailedComparison(Image1, Image2); //create methods for each
                    return DetailedComparisonResult;
                default:
                    throw new Exception();
            }
        }

        private DoveEyeImageComparisionResult BasicComparison(DoveEyeImage Image1, DoveEyeImage Image2)
        {
            double TimeDifference = Image1.dateTaken.Subtract(Image2.dateTaken).TotalMilliseconds;

            if (Math.Abs(TimeDifference) > 60000) //1 minutes, should be changed.
            {
                DoveEyeImageComparisionResult comparisonResult = new DoveEyeImageComparisionResult(ImageComparisonType.Basic, Image1, Image2, false, 0);
                return comparisonResult;
            }
            else
            {
                DoveEyeImageComparisionResult comparisonResult = new DoveEyeImageComparisionResult(ImageComparisonType.Basic, Image1, Image2, true, 0);
                return comparisonResult;
            }
#warning untested code. check if datetime totalms returns negative values or causes errors
        }
        private DoveEyeImageComparisionResult DetailedComparison(DoveEyeImage Image1, DoveEyeImage Image2)
        {
            //Match Features. Switch on Value. 
            KNearestNeighborMatching matching = new KNearestNeighborMatching(25);

            double MatchPoints = 0;
            double totalpoints = Image1.ImageFeatures.Count;
            double SDVectorLength = 0;
            double SDVectorDir = 0;
            if (Image1.ImageFeatures.Count == 0 || Image2.ImageFeatures.Count == 0)
            {
                //no features were found on one of them. assume different 
                DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, false, -1);
                return result;
            }
            else
            {
                Accord.IntPoint[][] results = matching.Match(Image1.ImageFeatures, Image2.ImageFeatures);
                MatchPoints = results[0].Length;
                List<DoveEyeFeatureVector> FeatureVectors = new List<DoveEyeFeatureVector>();
                for (int i = 0; i < results[0].Length; i++)
                {
                    FeatureVectors.Add(new DoveEyeFeatureVector(results[0][i].X, results[0][i].Y, results[1][i].X, results[1][i].Y));
                }

                List<double> PolarVectorLengths = new List<double>();
                List<double> PolarVectorDirection = new List<double>(); //this is calculated incase further research finds it to be a better indicator of similarity 
                foreach (DoveEyeFeatureVector featureVector in FeatureVectors)
                {
                    PolarVectorLengths.Add(featureVector.PolarDistance);
                    PolarVectorDirection.Add(featureVector.PolarTheta);
                }


                SDVectorLength = Accord.Statistics.Measures.Skewness(PolarVectorLengths.ToArray());
                SDVectorDir = Accord.Statistics.Measures.Skewness(PolarVectorDirection.ToArray());
                if (SDVectorLength < 0.8)
                {
                    //false
                    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, false, SDVectorLength);
                    result.FeatureVectors = FeatureVectors;
                    return result;
                }
                else if (SDVectorLength < 1.2)
                {
                    ComparsionCheckWindow HumanPrompt = new ComparsionCheckWindow(Image1.Thumbnail, Image2.Thumbnail);
                    HumanPrompt.ShowDialog();
                    while (!HumanPrompt.ComparisonComplete) { Thread.Sleep(100); }
                    if (HumanPrompt.ComparisonOutcome)
                    {
                        //Similar
                        DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, true, SDVectorLength);
                        result.FeatureVectors = FeatureVectors;
                        return result;
                    }
                    else
                    {
                        //Not Similar
                        DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, false, SDVectorLength);
                        result.FeatureVectors = FeatureVectors;
                        return result;
                    }
                }
                else
                {
                    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, true, SDVectorLength);
                    result.FeatureVectors = FeatureVectors;
                    return result;
                }
            }

            throw new NotImplementedException();
        }




        public double GetSharpness(DoveEyeContextualImage.ImageArea area, string filepath)
        {
            //finds the sharpness of a specific area in an image.
            using (MagickImage image = new MagickImage(filepath))
            {
                MagickGeometry CropRegion = new MagickGeometry(area.X, area.Y, area.Width, area.Height);
                image.Crop(CropRegion);

                //Resize to square and compute sharpness
                int size = getResizeSize(area.Width, area.Height);

                //Create Bitmap
                Bitmap Region;
                using (MemoryStream ms = new MemoryStream(image.ToByteArray()))
#warning could lead to memory overflow issues
                {
                    Region = new Bitmap(ms);
                }
                Bitmap AnalysisRegion = new Bitmap(Region, size, size);
                Region.Dispose();
                //apply Sharpness Detection Algorithm
                ComplexImage complexImage = ComplexImage.FromBitmap(AnalysisRegion.Clone(new Rectangle(0, 0, size, size), System.Drawing.Imaging.PixelFormat.Format8bppIndexed));
                complexImage.ForwardFourierTransform();
                Accord.Imaging.ComplexFilters.FrequencyFilter filter = new Accord.Imaging.ComplexFilters.FrequencyFilter(new Accord.IntRange(0, size / 4));


                filter.Apply(complexImage);

                double TotalSharpness = 0;

                ImageConverter converter = new ImageConverter();
                byte[] ComplexToBytes = (byte[])converter.ConvertTo(complexImage.ToBitmap(), typeof(byte[]));

                foreach (byte bite in ComplexToBytes)
                {
                    TotalSharpness += bite;
                }
#warning untested code

                return TotalSharpness;
            }

            int getResizeSize(int width, int height)
            {
                int power = 15;
                while (Math.Pow(2, power) > width || Math.Pow(2, power) > height)
                {
                    power--;
                }
                return (int)Math.Pow(2, power + 1);
            }
        }




        public List<DoveEyeImageFileInformation> getImages(string root)
        {
            string[] files = Directory.GetFiles(root);
            List<FileInfo> ImageFileInfo = new List<FileInfo>();
            foreach (string filepath in files)
            {
                ImageFileInfo.Add(new FileInfo(filepath));
            }

            //Eliminate file paths that aren't supported by ImageMagick
            foreach (FileInfo fileInfo in ImageFileInfo)
            {
#warning unimplemented code
                try
                {
                    //MagickImageInfo readTest = new MagickImageInfo(fileInfo.FullName); 

                }
                catch
                {
                    //reading failed, delete the fileinfo
                    ImageFileInfo.Remove(fileInfo);
                }
            }

            //Now consolidate the results.
            List<(string, List<string>)> FileNamesNoExtension = new List<(string, List<string>)>();
            foreach (FileInfo fileInfo in ImageFileInfo)
            {
                FileNamesNoExtension.Add(((fileInfo.Name.Substring(0, fileInfo.Name.Length - fileInfo.Extension.Length)), new List<string>()));
            }
            List<(string, List<string>)> FileNamesDistinctNoExt = new List<(string, List<string>)>();
            List<string> tempFileNames = new List<string>();

            foreach ((string, List<string>) fileInfo1 in FileNamesNoExtension)
            {
                if (!tempFileNames.Contains(fileInfo1.Item1))
                {
                    tempFileNames.Add(fileInfo1.Item1);
                    FileNamesDistinctNoExt.Add(fileInfo1);
                }
            }

            //put the extensions together. (string = filename no extension, list<string> possible extensions) (ex: ("IMG_blah",".jpg",".cr2")
            for (int i = 0; i < FileNamesNoExtension.Count; i++)
            {
                for (int j = 0; j < FileNamesDistinctNoExt.Count; j++)
                {
                    if (FileNamesDistinctNoExt[j].Item1 == FileNamesNoExtension[i].Item1)
                    {
                        FileNamesDistinctNoExt[j].Item2.Add(ImageFileInfo[i].Extension);
                    }
                }
            }

            List<DoveEyeImageFileInformation> doveEyeImageFiles = new List<DoveEyeImageFileInformation>();

            for (int i = 0; i < FileNamesDistinctNoExt.Count; i++)
            {
                (string, List<string>) ImageFile = FileNamesDistinctNoExt[i];
                doveEyeImageFiles.Add(new DoveEyeImageFileInformation(root, ImageFile.Item1, ImageFile.Item2, i));
            }

            return doveEyeImageFiles;
        }




        public List<DoveEyeImageGroup> GetGroupings(ImageComparisonType comparisonType, List<DoveEyeContextualImage> Images)
        {
            //core code is functional, but should be put into a class for asynchronous execution
            //code to get the groupings based on an ImageComparisonType
            List<DoveEyeImageGroup> Groupings = new List<DoveEyeImageGroup>();
            List<DoveEyeContextualImage> TemporaryBuffer = new List<DoveEyeContextualImage>();
            for (int i = 0; i < Images.Count - 1; i++)
            {
                DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                DoveEyeImageComparisionResult comparisonResult = processor.CompareImages(Images[i].Image, Images[i + 1].Image, comparisonType);

                if (comparisonResult.Similar)
                {
                    TemporaryBuffer.Add(Images[i]);
                    //add to current grouping.
                }
                else
                {
                    TemporaryBuffer.Add(Images[i]);
                    DoveEyeImageGroup ImageGroup = new DoveEyeImageGroup();
                    ImageGroup.Images = TemporaryBuffer;
                    TemporaryBuffer = new List<DoveEyeContextualImage>();
                    ImageGroup.GroupIndex = Groupings.Count();

                    Groupings.Add(ImageGroup);
                }
            }

            return Groupings;
        }



        public class DoveEyeGroupingManager
        {
            public readonly ImageComparisonType ComparisonType;
            public List<DoveEyeContextualImage> Images;

            public int TotalImages;
            public int Progress = 0;

            public bool AnalysisComplete = false;

            public List<DoveEyeImageGroup> Groupings = new List<DoveEyeImageGroup>();

            public DoveEyeGroupingManager(ImageComparisonType cmpType, List<DoveEyeContextualImage> imgs)
            {
                ComparisonType = cmpType;
                Images = imgs;
                TotalImages = imgs.Count;
            }

            public void BeginGroupAnalysis()
            {
                List<DoveEyeContextualImage> TemporaryBuffer = new List<DoveEyeContextualImage>();
                for (int i = 0; i < Images.Count - 1; i++)
                {
                    DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                    DoveEyeImageComparisionResult comparisonResult = processor.CompareImages(Images[i].Image, Images[i + 1].Image, ComparisonType);

                    if (comparisonResult.Similar)
                    {
                        TemporaryBuffer.Add(Images[i]);
                        //add to current grouping.
                    }
                    else
                    {
                        TemporaryBuffer.Add(Images[i]);
                        DoveEyeImageGroup ImageGroup = new DoveEyeImageGroup();
                        ImageGroup.Images = TemporaryBuffer;
                        TemporaryBuffer = new List<DoveEyeContextualImage>();
                        ImageGroup.GroupIndex = Groupings.Count();

                        Groupings.Add(ImageGroup);
                    }

                    Progress++;
                }
                AnalysisComplete = true;
            }
        }

        public class DoveEyeAnalysisManager
        {
            //contains all code to efficiently analyze files.
            //Potential issues: Low memory usage will cause massive problems as imagemagick starts writing to a page file.
            string root;
            public int TotalFiles;
            public int ReadProgress;
            public int AnalysisProgress;

            public List<DoveEyeContextualImage> Images = new List<DoveEyeContextualImage>();
            public List<DoveEyeImageFileInformation> ImageFileInformation;


            List<DisposalState> FileDisposalStates = new List<DisposalState>();
            List<FileStream> FileStreams = new List<FileStream>();

            int FileQueue;
            int fileReadIndex;

            public bool AnalysisComplete;

            int ThreadCount;
            int QueueBuffer;

            public DoveEyeAnalysisManager(string rootfile, int threads, int queuecount)
            {
                root = rootfile;

                ThreadCount = threads;
                QueueBuffer = queuecount;
            }
            private enum DisposalState
            {
                Active,
                AwaitingOrders,
                Disposed,
                Unread
            }

            public void BeginAnalysis()
            {
                ResourceLimits.LimitMemory(new Percentage(1.00));
                DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                ImageFileInformation = processor.getImages(root);



                int readfilesIndex = 0;

                for (int i = 0; i < ImageFileInformation.Count; i++) { FileDisposalStates.Add(DisposalState.Unread); }

                TotalFiles = ImageFileInformation.Count;
                FileQueue = ThreadCount + QueueBuffer;


                //Begin FileStream Manager Thread
                Thread FileManagerThread = new Thread(new ThreadStart(FileStreamManager));
                FileManagerThread.Start();
                //Begin Analysis Manager
                Thread AnalysisManagerThread = new Thread(new ThreadStart(ImageAnalysisManager));
                AnalysisManagerThread.Start();
                //Begin Memory Manager
                Thread MemorymanagerThread = new Thread(new ThreadStart(MemoryManager));
                MemorymanagerThread.Start();
                //Begin Progress Updater Thread
                Thread ProgressUpdaterThread = new Thread(new ThreadStart(ProgressUpdater));
                ProgressUpdaterThread.Start();
            }
            void ProgressUpdater()
            {
                while (!AnalysisComplete)
                {
                    int tempReadProgress = 0;
                    int tempAnalysisProgress = 0;
                    for (int i = 0; i < FileDisposalStates.Count; i++)
                    {
                        DisposalState state = FileDisposalStates[i];
                        if (state != DisposalState.Unread)
                        {
                            tempReadProgress++;
                        }
                    }

                    for (int i = 0; i < Images.Count; i++)
                    {
                        try
                        {
                            if (Images[i].Image.AnalysisComplete)
                            {
                                tempAnalysisProgress++;
                            }
                        }
                        catch { }
                    }

                    ReadProgress = tempReadProgress;
                    AnalysisProgress = tempAnalysisProgress;

                    if (AnalysisProgress >= TotalFiles) { AnalysisComplete = true; }
                    Thread.Sleep(200);
                }
            }

            void MemoryManager()
            {
                while (!AnalysisComplete)
                {
                    for (int i = 0; i < Images.Count; i++)
                    {
                        //check if the image is done analyzing. If it is, destroy it.
                        if (Images[i].Image.FileUsageComplete && FileDisposalStates[i] == DisposalState.Active)
                        {
                            FileStreams[i].Dispose();
                            FileDisposalStates[i] = DisposalState.Disposed;
                        }
                    }
                    Thread.Sleep(200);
                }
            }

            void ImageAnalysisManager()
            {
                while (!AnalysisComplete)
                {
                    if (GetActiveAnalyses() < ThreadCount && FileDisposalStates.Contains(DisposalState.AwaitingOrders))
                    {
                        //Find the next file that is awaiting orders, and read create an image out of it.
                        for (int i = 0; i < FileDisposalStates.Count; i++)
                        {
                            if (FileDisposalStates[i] == DisposalState.AwaitingOrders)
                            {
                                Images.Add(new DoveEyeContextualImage(ImageFileInformation[i], FileStreams[i]));
                                FileDisposalStates[i] = DisposalState.Active;

                                break;
                            }
                        }
                    }

                    Thread.Sleep(200);
                }

                int GetActiveAnalyses()
                {
                    int activethreads = 0;
                    for (int i = 0; i < FileDisposalStates.Count; i++)
                    {
                        activethreads += FileDisposalStates[i] == DisposalState.Active ? 1 : 0;
                    }
                    return activethreads;
                }
            }

            void FileStreamManager()
            {
                while (!AnalysisComplete)
                {
                    if (getactivefiles() < FileQueue && fileReadIndex < TotalFiles)
                    {
                        //Need to support Queue. Add to FileStreams
                        FileStreams.Add(new FileStream(ImageFileInformation[fileReadIndex].FullFilePathPreferred, FileMode.Open, FileAccess.Read));
                        FileDisposalStates[fileReadIndex] = DisposalState.AwaitingOrders;
                        fileReadIndex++;
                    }

                    Thread.Sleep(500);
                }

                int getactivefiles()
                {
                    int returnvalue = 0;
                    for (int i = 0; i < FileDisposalStates.Count; i++)
                    {
                        DisposalState disposalState = FileDisposalStates[i];
                        returnvalue += (disposalState == DisposalState.Active || disposalState == DisposalState.AwaitingOrders) ? 1 : 0;
                    }
                    return returnvalue;
                }
            }
        }
    }
}