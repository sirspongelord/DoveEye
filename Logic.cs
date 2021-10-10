using System;
using System.Drawing;
using Accord.Imaging;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.Generic;
using Accord.Imaging.Filters;
using System.Threading;
using Accord.Statistics;
using Accord.Vision;
using Accord.Vision.Detection;
using ImageMagick;
using System.Linq;
using DoveVision;
using System.Threading.Tasks;
using static DoveVision.DoveEyeImageProcessor;
using DoveEye;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows;
using System.ComponentModel;
using System.Drawing.Imaging;
using Accord.MachineLearning;
using static DoveEye.ComparsionCheckWindow;
using System.Collections.ObjectModel;

namespace DoveVision
{
    //contains all backend for the program.

    //DoveEyeLogic contains the following structure:
    /** class DoveEyeImageCanvas {}
     *      class DoveEyeImageGroup {}
     *          class DoveEyeContextualImage {}
     *              class DoveEyeImage {}
     *              
     *  This structure is utilized to organize the images that are the subject of the analysis.
     *  - Each image, along with its objective information (such as its filepath, size, etc.) is stored in DoveEyeImage {}
     *  - Each DoveEyeImage is stored inside class DoveEyeContextualImage {}, along with other user-specific information, such as whether the image is kept or deleted, etc.
     *  
     *  Think of it like this: If all you have is the image file, you can construct the DoveEyeImage.
     *  If you need to ask the user for some information, or do processing that involves multiple images, those properties are stored in the Contextual Image.
     *  
     *  A list of Contextual Images constitutes one DoveEyeImageGroup, along with related methods/code.
     *  
     *  A list of DoveEyeImageGroup constitutes the ImageCanvas - the class that contains all information about the image.
     *  
     *  
     *  
     *  class DoveEyeImageProcessing {} contains all PROCESSING information 
     *      - example: Analyzing image sharpness
     *      - example: Grouping the images together. 
     */
    public class DoveEyeContextualImage : IEquatable<DoveEyeContextualImage>, IComparable<DoveEyeContextualImage>, INotifyPropertyChanged
    {
        //Includes a DoveEyeImage with relevant contextual properties set by the user.

        public DoveEyeImage Image { get; set; }

        public int ImageIndex;
        public int ImageGroup;
        public FileInfo ImageInfo;

        public List<string> QualityStates { get; set; }
        public int QualitySelectedIndex { get; set; } //potentially needs to be set to a default value.

            
        //Sharpness Area. Default is the "Smart" area, though can be overwritten as center.

        public double Sharpness
        {
            get
            {
                return Image.Sharpness;
            }
        }

        //ExtensionStates, class ImageState and ImageStates are not used in the program and can be deleted.
        //The intent was to allow file-extension level control for each image, but that is too difficult.
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

        public List<ImageState> ImageStates { get; set; }


        //Constructor
        public DoveEyeContextualImage(DoveEyeImageFileInformation fileInfo, FileStream stream, DoveEyeImage.SharpnessAreatype sharpnessAnalysisType, double ScalePercentage)
        {
            QualityStates = new List<string>();
            QualityStates.Add("High Quality");
            QualityStates.Add("Low Quality");

            ResourceLimits.LimitMemory(new Percentage(1.00));
            string FilePath = fileInfo.FilePath + fileInfo.FileName;
            List<string> extensions = fileInfo.Extensions;


            //consider passingdoveeyeimagefileinformation to this ??
            Image = new DoveEyeImage(FilePath, ScalePercentage, extensions, stream, fileInfo.BestFileInfo, sharpnessAnalysisType, fileInfo);

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



        FileStream MemoryFile;
        public void ComputeSharpness(FileStream file)
        {
            //OBSOLETE | SHOULD NEVER BE CALLED
            //This can and should be done using a task, but this works too and I don't feel like googling how tasks work.
            this.MemoryFile = file;

            Thread AnalysisThread = new Thread(new ThreadStart(AnalyzeSharpnessThread));
            AnalysisThread.Start();
        }

        void AnalyzeSharpnessThread()
        {
            DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
            //PrivateSharpness = processor.GetSharpness(CustomSharpnessArea, MemoryFile);
            MemoryFile.Dispose();
            Image.AnalysisComplete = true;
            Image.FileUsageComplete = true;
            GC.Collect();
        }

        public bool Equals(DoveEyeContextualImage other)
        {
            return (this == other) ? true : false;
        }

        public enum ImageSortOption
        {
            Sharpness,
            Index,
            Exposure
        }
        public ImageSortOption sortOption = ImageSortOption.Sharpness;
        public int CompareTo(DoveEyeContextualImage other)
        {
            switch(sortOption)
            {
                case ImageSortOption.Sharpness:
                    return this.Sharpness > other.Sharpness ? -1 : 1;
                    break;
                case ImageSortOption.Exposure:
                    return this.Image.Exposure > other.Image.Exposure ? 1 : -1;
                    break;
                case ImageSortOption.Index:
                    return this.ImageIndex > other.ImageIndex ? 1 : -1;
                    break;
                default:
                    throw new Exception("something went seriously wrong");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }

    public class DoveEyeImage
    {
        //Includes only objective information about the image. No User-Defined Context exists.
        public string FileName { get; set; } //Name of File
        public string FilePath { get; set; }
        public Bitmap Thumbnail { 
            get
            {
                return new Bitmap(bmpFileSource);
            } 
        }
        public string bmpFileSource;

        public double Sharpness { get; private set; }
        public BitmapSource DisplayThumbnail
        {
            get; set;
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


        public enum SharpnessAreatype
        {
            Center,
            Full,
            Auto,
            FacialFallBackAuto
        }
        SharpnessAreatype areaType;



        public readonly double ProcessingScale; //for thumbnail and feature point size 

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


        public ImageArea sharpnessArea { get
            {
                switch(areaType)
                {
                    case SharpnessAreatype.Center:
                        return centerSharpenssArea;
                    case SharpnessAreatype.Auto:
                        return autoSharpnessArea;
                    case SharpnessAreatype.Full:
                        return fullSharpnessArea;
                    case SharpnessAreatype.FacialFallBackAuto:
                        if(faceSharpnessArea.Equals(new ImageArea(-1,-1,-1,-1)))
                        {
                            return autoSharpnessArea;
                        }
                        else
                        {
                            return faceSharpnessArea;
                        }
                    default:
                        throw new Exception();
                }
            } }

        private ImageArea autoSharpnessArea { get; set; }
        private ImageArea fullSharpnessArea { get; set; }
        private ImageArea centerSharpenssArea { get; set; }

        private ImageArea faceSharpnessArea { get; set; }



        private List<SpeededUpRobustFeaturePoint> PrivateImageFeatures;
        void ImageCreationThread()
        {
            file.Position = 0;
            GC.Collect(); //this is not the best way to do this
            MagickImage tempMagickImage;
            try
            {
                tempMagickImage = new MagickImage(file);
            } catch
            {
                try { tempMagickImage = new MagickImage(fileinformation.FullFilePathPreferred); } 
                catch 
                {
                    for (int i = 1; i < 11; i++)
                    {
                        MessageBox.Show("An error occurred while opening the file at " + fileinformation.FileName + ". Try clearing your system memory or hard disk space to prevent this issue. Retrying... Attempt " + i + " of 10");
                        try { tempMagickImage = new MagickImage(fileinformation.FullFilePathPreferred); return; }
                        catch { }
                    }
                    throw new Exception();
                }                
            }
            
            MagickImage secondMagickImage = new MagickImage(tempMagickImage);

            FileUsageComplete = true;
            file.Dispose();

            //store original image width/height
            width = tempMagickImage.Width;
            height = tempMagickImage.Height;

            //EXIF reading does not work correctly. Feature is temporarily disabled.
            //IExifProfile exifProfile = tempMagickImage.GetExifProfile();
            //IExifValue DateTimeOriginalTaken = exifProfile.GetValue(ExifTag.DateTimeOriginal);
            //IExifValue ExifAperture = exifProfile.GetValue(ExifTag.ApertureValue);
            //IExifValue ExifISO = exifProfile.GetValue(ExifTag.ISOSpeed);
            //IExifValue ExifShutterSpeed = exifProfile.GetValue(ExifTag.ShutterSpeedValue);

            //Resize Image, Store Thumbnail
            tempMagickImage.Resize(new Percentage(ProcessingScale));
            Bitmap bmpThumb;
            using (MemoryStream memstream = new MemoryStream())
            {
                tempMagickImage.Write(memstream, MagickFormat.Jpg);
                bmpThumb = new Bitmap(memstream);
                string fileSource = Path.GetTempFileName();
                bmpThumb.Save(fileSource);
                bmpFileSource = fileSource;
                memstream.Dispose();
            }
            tempMagickImage.Dispose();

            //Analyze the features
            DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
            PrivateImageFeatures = processor.GetFeatures(bmpThumb, ProcessingScale);

            //Analyze Sharpness
            autoSharpnessArea = processor.getAutoSharpnessArea(ImageFeatures, ProcessingScale, width, height);
            if (areaType == SharpnessAreatype.FacialFallBackAuto) { int minSize = Math.Min(width, height) / 72; faceSharpnessArea = processor.getFaceSharpnessArea(bmpThumb, ProcessingScale, minSize, width, height); }
            else { faceSharpnessArea = new ImageArea(-1, -1, -1, -1); }
            fullSharpnessArea = processor.getFullSharpnessArea(width, height);
            centerSharpenssArea = processor.getFullSharpnessArea(width, height);

            if(PrivateImageFeatures.Count < 5) { areaType = SharpnessAreatype.Center; }

            //Sharpness = processor.GetSharpness(sharpnessArea, bmpFileSource);
            Sharpness = processor.GetSharpness(sharpnessArea, secondMagickImage);
            secondMagickImage.Dispose();

            DisplayThumbnail = Bitmap2BitmapImage(bmpThumb);
            DisplayThumbnail.Freeze();

            //histogram from thumbnail (can be improved later)
            ImageStatistics imageStatistics = new ImageStatistics(bmpThumb);
            Histogram = new DoveEyeHistogram(imageStatistics);

            //EXIF READING DOES NOT WORK!!
            
            //Aperture = "Not Implemented";
            //ISOSpeed = ExifISO.ToString();
            //ShutterSpeed = ExifShutterSpeed.ToString();
            //dateTaken = DateTime.Parse(DateTimeOriginalTaken.ToString()); //!! Likely to fail !!

            //Dispose Image



            //exposure
            Exposure = (Histogram.Red.Mean + Histogram.Blue.Mean + Histogram.Green.Mean) / 3;
            AnalysisComplete = true;

        }

        FileStream file;
        public DoveEyeImageFileInformation fileinformation;

        public DoveEyeImage(string filepath, double ScalePercentageHighThumbnail, List<string> extensions, FileStream stream, FileInfo info, SharpnessAreatype sharpnessAreatype, DoveEyeImageFileInformation fileInfo)
        {
            ResourceLimits.LimitMemory(new Percentage(1.00));
            try
            {
                Thread.Sleep(500);
                fileinformation = fileInfo;
                file = stream;
                areaType = sharpnessAreatype;
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
        //image comparator. Currently only supports basic (datetime comparison) and detailed (featurematching comparison)

        //potentially should be moved inside ImageProcessor
        Basic,
        Detailed
        //FeaturePoint,
        //FeaturePointVectors,
        //DateTimeDifference,
        //ColorSimilarity
    }
    public struct DoveEyeFeatureVector
    {
        //Used to store the FeaturePoint
        //potentially should be moved inside ImageProcessor
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

        //potentially should be moved inside ImageProcessor
        public double X;
        public double Y;
        public DoveEyeFeaturePoint(double X, double Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
    public class DoveEyeImageComparisionResult
    {
        //returned by CompareImages theoretically.
        public ImageComparisonType ImageComparisonType;
        public double ComparisonValue;
        public bool Similar;
        public ComparisonResult result;
        public enum ComparisonResult
        {
            Similar,
            NotSimilar,
            HumanPromptRequired,
            Error
        }
        public bool UsedFallback = false; //Not Implemented

        public List<SpeededUpRobustFeaturePoint> Image1Features;
        public List<SpeededUpRobustFeaturePoint> Image2Features;

        public List<DoveEyeFeatureVector> FeatureVectors;

        public DoveEyeImageComparisionResult(ImageComparisonType ComparisonType, DoveEyeImage Image1, DoveEyeImage Image2, ComparisonResult result, double comparisonValue)
        {
            ImageComparisonType = ComparisonType;
            Image1Features = Image1.ImageFeatures;
            Image2Features = Image2.ImageFeatures;

            this.result = result;

            ComparisonValue = comparisonValue;

            FeatureVectors = new List<DoveEyeFeatureVector>(); //set to a non-empty list if relevant.
        }

    }


    public class DoveEyeImageFileInformation
    {

        //potentially should be moved inside ImageProcessor
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


    public class DoveEyeImageGroup : INotifyPropertyChanged
    {

        //potentially should be moved inside ImageProcessor
        //information in a specific grouping
        public List<DoveEyeContextualImage> Images { get; set; }

        public int GroupIndex { get; set; }
        public string GroupName { get; set; }

        public double MaxSharpness
        {
            get
            {
                double returnvalue = 0;
                foreach (DoveEyeContextualImage image in Images)
                {
                    if(image.Image.Sharpness > returnvalue) { returnvalue = image.Image.Sharpness; }
                }
                return returnvalue;
            }
        }
        public double MinSharpness
        {
            get
            {
                double returnvalue = 10E+15;
                foreach (DoveEyeContextualImage image in Images)
                {
                    if (image.Image.Sharpness < returnvalue) { returnvalue = image.Image.Sharpness; }
                }
                return returnvalue;
            }
        }


        public double MaxExposure
        {
            get
            {
                double returnvalue = 0;
                foreach (DoveEyeContextualImage image in Images)
                {
                    if (image.Image.Exposure > returnvalue) { returnvalue = image.Image.Exposure; }
                }
                return returnvalue;
            }
        }
        public double MinExposure
        {
            get
            {
                double returnvalue = 10E+10;
                foreach (DoveEyeContextualImage image in Images)
                {
                    if (image.Image.Exposure < returnvalue) { returnvalue = image.Image.Exposure; }
                }
                return returnvalue;
            }
        }

        public void SortBySharpness()
        {
            for (int i = 0; i < Images.Count; i++)
            {
                Images[i].sortOption = DoveEyeContextualImage.ImageSortOption.Sharpness;
            }
            Images.Sort();
            OnPropertyChanged();
        }

        public void SortByExposure()
        {
            foreach (DoveEyeContextualImage image in Images)
            {
                image.sortOption = DoveEyeContextualImage.ImageSortOption.Exposure;
            }
            Images.Sort();
            OnPropertyChanged();
        }

        public void SortByIndex()
        {
            for (int i = 0; i < Images.Count; i++)
            {
                Images[i].sortOption = DoveEyeContextualImage.ImageSortOption.Index;
            }
            Images.Sort();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        
        public void OnPropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        
        public void ReassignIndices()
        {
            for(int i = 0; i < Images.Count; i++)
            {
                Images[i].ImageIndex = i;
            }
        }
    }

    public class DoveEyeImageCanvas
    {
        //name can be do better maybe, this works too.
        public string root { get; set; }
        public List<DoveEyeImageFileInformation> ImageFiles { get; set; }
        public int TotalImages
        {
            get
            {
                int returnval = 0; 
                foreach (DoveEyeImageGroup group in ImageGroups)
                {
                    returnval += group.Images.Count;
                }
                return returnval;
            }
        }

        DoveEyeImage.SharpnessAreatype sharpnessAreaType;

        int threads;
        int buffer;

        public DoveEyeGroupingManager GroupingManager { get; set; }
        private List<DoveEyeContextualImage> images
        {
            get
            {
                List<DoveEyeContextualImage> tempimages = new List<DoveEyeContextualImage>();
                foreach (DoveEyeImageGroup group in ImageGroups)
                {
                    tempimages.AddRange(group.Images);
                }
                return tempimages;
            }
        }

        public DoveEyeImageCanvas(string root, int threads, int buffer, DoveEyeImage.SharpnessAreatype areaType, List<DoveEyeImageFileInformation> ImageFileInformation, double ScalePercentageForThumb)
        {
            this.root = root;
            this.threads = threads;
            this.buffer = buffer;
            this.sharpnessAreaType = areaType;

            AnalysisManager = new DoveEyeAnalysisManager(root, threads, buffer, sharpnessAreaType, ScalePercentageForThumb);
            AnalysisManager.ImageFileInformation = ImageFileInformation;
        }

        public ObservableCollection<DoveEyeImageGroup> ImageGroups { get; set; }

        public DoveEyeAnalysisManager AnalysisManager { get; set; }

        public void MergeGroups(int index1, int index2)
        {
            int lowerindex = Math.Min(index1, index2);
            int higherindex = Math.Max(index1, index2);
            DoveEyeImageGroup MergedGroups = new DoveEyeImageGroup();

            ImageGroups[lowerindex].SortByIndex();
            ImageGroups[higherindex].SortByIndex();

            MergedGroups.Images = new List<DoveEyeContextualImage>();
            MergedGroups.Images.AddRange(ImageGroups[lowerindex].Images);
            MergedGroups.Images.AddRange(ImageGroups[higherindex].Images);

            MergedGroups.GroupName = lowerindex + "," + higherindex;
            MergedGroups.GroupIndex = -1;
            //reassign indeces for the group.
            MergedGroups.ReassignIndices();
            MergedGroups.SortByIndex();

            ImageGroups.Remove(ImageGroups[lowerindex]);
            ImageGroups.Remove(ImageGroups[lowerindex]);

            ImageGroups.Insert(lowerindex,MergedGroups);

            ConsolidateIndices();
        }
        void ConsolidateIndices()
        {
            for (int i = 0; i < ImageGroups.Count; i++)
            {
                ImageGroups[i].GroupIndex = i;
            }
        }

        public void SplitGroup(int GroupIndex, int ImageIndex)
        {
            List<DoveEyeContextualImage> group1Images = new List<DoveEyeContextualImage>();
            group1Images.AddRange(ImageGroups[GroupIndex].Images.GetRange(0, ImageIndex));
            List<DoveEyeContextualImage> group2Images = new List<DoveEyeContextualImage>();
            group2Images.AddRange(ImageGroups[GroupIndex].Images.GetRange(ImageIndex,  ImageGroups[GroupIndex].Images.Count-ImageIndex));

            DoveEyeImageGroup group1 = new DoveEyeImageGroup();
            group1.Images = group1Images;
            group1.GroupName = GroupIndex + "Split 1";

            DoveEyeImageGroup group2 = new DoveEyeImageGroup();
            group2.Images = group2Images;
            group2.GroupName = GroupIndex + "Split 2";
            
            ImageGroups.Remove(ImageGroups[GroupIndex]);
            ImageGroups.Insert(GroupIndex, group2);
            ImageGroups.Insert(GroupIndex, group1);
            ConsolidateIndices();
        }


        public void AnalyzeImages()
        {
            AnalysisManager.BeginAnalysis();
        }


        public void AnalyzeGroupings()
        {
            //Note: imagecomparisontype selection is not yet implemented.
            GroupingManager = new DoveEyeGroupingManager(ImageComparisonType.Detailed, AnalysisManager.Images, threads);
            GroupingManager.BeginGroupAnalysis();
        }

        public void AssignIndices()
        {
            foreach (DoveEyeImageGroup group in ImageGroups)
            {
                for(int i = 0; i < group.Images.Count; i++)
                {
                    group.Images[i].ImageIndex = i;
                }
            }
        }

        public void SortBySharpness()
        {
            //sort each group by sharpness.

            foreach (DoveEyeImageGroup group in ImageGroups)
            {
                group.SortBySharpness();
            }
        }
        public void SortByExposure()
        {
            foreach (DoveEyeImageGroup group in ImageGroups)
            {
                group.SortByExposure();
            }
        }
        public void SortByIndex()
        {
            //sort each group by index.

            foreach (DoveEyeImageGroup group in ImageGroups)
            {
                group.SortByIndex();
            }
        }

        private class SharpnessAnalysisManager
        {
            //OBSOLETE | SHOULD BE REMOVED
            //This class analyses the sharpness using a multi-threaded system very similar to the the analysismanager in processor.

            //Most of the code is reused. Some stuff has been changed. Who knows if this works or not.
            string root;
            public int TotalFiles;
            public int ReadProgress;
            public int AnalysisProgress;

            public List<DoveEyeContextualImage> Images = new List<DoveEyeContextualImage>();


            List<DisposalState> FileDisposalStates = new List<DisposalState>();
            List<FileStream> FileStreams = new List<FileStream>();

            int FileQueue;
            int fileReadIndex;

            public bool AnalysisComplete;

            int ThreadCount;
            int QueueBuffer;

            public SharpnessAnalysisManager(List<DoveEyeContextualImage> images, int threads, int queuecount)
            {
                Images = images;

                ThreadCount = threads;
                QueueBuffer = queuecount;

                for (int i = 0; i < Images.Count; i++)
                {
                    images[i].Image.FileUsageComplete = false;
                    images[i].Image.AnalysisComplete = false;
                }
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


                    int readfilesIndex = 0;

                    for (int i = 0; i < Images.Count; i++) { FileDisposalStates.Add(DisposalState.Unread); }

                    TotalFiles = Images.Count;
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
                                Images[i].ComputeSharpness(FileStreams[i]);
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
                        FileStreams.Add(new FileStream(Images[fileReadIndex].Image.FilePath, FileMode.Open, FileAccess.Read));
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


    public class DoveEyeImageProcessor
    {
        //Contains image processing information.

        //DoveEye analyzes images using two separate analysis managers.
        /** class DoveEyeGroupingManager {} manages the process of identifying image groups.
         *  class DoveEyeAnalysisManager {} manages the process of analyzing a group of images.
         *  
         *  These two classes are used for basically everything that this class does.
         */
        public class DoveEyeComparisonRequest
        {
            DoveEyeContextualImage Image1;
            DoveEyeContextualImage Image2;
            ImageComparisonType comparisonType;

            public DoveEyeImageComparisionResult result;
            public DoveEyeComparisonRequest(DoveEyeContextualImage Image1, DoveEyeContextualImage Image2, ImageComparisonType comparisonType)
            {
                this.Image1 = Image1;
                this.Image2 = Image2;
                this.comparisonType = comparisonType;
            }

            public DoveEyeComparisonRequest(DoveEyeContextualImage Image1, DoveEyeContextualImage Image2, ImageComparisonType comparisonType, ImageComparisonType fallbackComparisonType)
            {

            }
        }


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
                DoveEyeImageComparisionResult comparisonResult = new DoveEyeImageComparisionResult(ImageComparisonType.Basic, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.NotSimilar, 0);
                return comparisonResult;
            }
            else
            {
                DoveEyeImageComparisionResult comparisonResult = new DoveEyeImageComparisionResult(ImageComparisonType.Basic, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.Similar, 0);
                return comparisonResult;
            }
        }
        private DoveEyeImageComparisionResult DetailedComparison(DoveEyeImage Image1, DoveEyeImage Image2)
        {
            //Match Features. Switch on Value. 
            int minFeatures = Math.Min(65, Math.Min(Image1.ImageFeatures.Count, Image2.ImageFeatures.Count));
            KNearestNeighborMatching matching = new KNearestNeighborMatching(minFeatures);

            double MatchPoints = 0;
            double totalpoints = Image1.ImageFeatures.Count;
            double SDVectorLength = 0;
            double SDVectorDir = 0;
            if (Image1.ImageFeatures.Count == 0 || Image2.ImageFeatures.Count == 0)
            {
                //no features were found on one of them. assume different 
                DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.NotSimilar, -1);
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
                if (SDVectorLength < 0.55)
                {
                    //false
                    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.NotSimilar, SDVectorLength);
                    result.FeatureVectors = FeatureVectors;
                    return result;
                }
                else if (SDVectorLength < 1.0)
                {
                    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.HumanPromptRequired, SDVectorLength);
                    return result;
                    //ComparsionCheckWindow HumanPrompt = new ComparsionCheckWindow(Image1, Image2);
                    //HumanPrompt.ShowDialog();
                    //while (!HumanPrompt.ComparisonComplete) { Thread.Sleep(100); }
                    //if (HumanPrompt.ComparisonOutcome)
                    //{
                    //    //Similar
                    //    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, true, SDVectorLength);
                    //    result.FeatureVectors = FeatureVectors;
                    //    return result;
                    //}
                    //else
                    //{
                    //    //Not Similar
                    //    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, false, SDVectorLength);
                    //    result.FeatureVectors = FeatureVectors;
                    //    return result;
                    //}
                }
                else
                {
                    DoveEyeImageComparisionResult result = new DoveEyeImageComparisionResult(ImageComparisonType.Detailed, Image1, Image2, DoveEyeImageComparisionResult.ComparisonResult.Similar, SDVectorLength);
                    result.FeatureVectors = FeatureVectors;
                    return result;
                }
            }
        }




        public struct ImageArea
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public ImageArea(int X, int Y, int Width, int Height)
            {
                this.X = X;
                this.Y = Y;
                this.Width = Width;
                this.Height = Height;
            }
        }

        public double GetSharpness(ImageArea area, FileStream file)
        {
            //finds the sharpness of a specific area in an image.

            MagickImage image = new MagickImage(file);
            
            MagickGeometry CropRegion = new MagickGeometry(area.X, area.Y, area.Width, area.Height);
            image.Crop(CropRegion);

            //Resize to square and compute sharpness
            int size = getResizeSize(area.Width, area.Height);

            //Create Bitmap
            Bitmap Region;
            using (MemoryStream memstream = new MemoryStream())
            {
                image.Write(memstream, MagickFormat.Jpg);
                Region = new Bitmap(memstream);
                memstream.Dispose();
            }
            Bitmap AnalysisRegion = new Bitmap(Region, size, size); //slaughter and kill the memory while you're at it.
            Region.Dispose();
            //apply Sharpness Detection Algorithm
            ComplexImage complexImage = ComplexImage.FromBitmap(AnalysisRegion.Clone(new Rectangle(0, 0, size, size), System.Drawing.Imaging.PixelFormat.Format8bppIndexed));
            AnalysisRegion.Dispose();
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

            

            return TotalSharpness / size;
            

            int getResizeSize(int width, int height)
            {
                int power = 15;
                while (Math.Pow(2, power) > width || Math.Pow(2, power) > height)
                {
                    power--;
                }
                return (int)Math.Pow(2, power);
            }
        }
        public double GetSharpness(ImageArea area, string thumbFileSource)
        {
            //NO LONGER USED
            Rectangle cropRect = new Rectangle(area.X/2, area.Y/2, area.Width/2, area.Height/2);
            MagickImage image = new MagickImage(thumbFileSource);
            MagickGeometry cropGeo = new MagickGeometry(area.X / 2, area.Y / 2, area.Width / 2, area.Height / 2);
            image.Crop(cropGeo);
            
            int size = getResizeSize(area.Width / 2, area.Height / 2);
            MagickGeometry resizeGeo = new MagickGeometry(size, size);
            resizeGeo.IgnoreAspectRatio = true;
            //image.AdaptiveResize(size, size);
            image.Resize(resizeGeo);
            //image.InterpolativeResize(size,size,PixelInterpolateMethod.Average);

            //Bitmap BMPStep1 = new Bitmap(cropRect.Width, cropRect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //using (Graphics g = Graphics.FromImage(BMPStep1))
            //{
            //    Bitmap BmpImage = new Bitmap(thumbFileSource);
            //    g.DrawImage(BmpImage, new Rectangle(0, 0, BMPStep1.Width, BMPStep1.Height),
            //                     cropRect,
            //                     GraphicsUnit.Pixel);
            //    BmpImage.Dispose();
            //}
            //
            //
            //Bitmap AnalysisRegion = new Bitmap(BMPStep1, size, size);
            //BMPStep1.Dispose();
            FileInfo tempFile = new FileInfo(Path.GetTempFileName());
            image.Write(tempFile,MagickFormat.Jpg);

            Bitmap AnalysisRegion = new Bitmap(tempFile.FullName);
            
            //using (MemoryStream memstream = new MemoryStream())
            //{
            //    image.Write(memstream, MagickFormat.Bmp);
            //    AnalysisRegion = new Bitmap(memstream);
            //    memstream.Dispose();
            //}
            image.Dispose();


#warning this is not fast.
            //apply Sharpness Detection Algorithm
            //Bitmap temp2 = new Bitmap(tempFile.FullName).Clone(System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            //Bitmap temp = new Bitmap(size, size,System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            //using (Graphics g = Graphics.FromImage(temp))
            //{
            //    g.DrawImage(new Bitmap(tempFile.FullName), new Rectangle(0, 0, size, size));
            //}
            //AnalysisRegion.Dispose();
            Bitmap newbmp;
            using (var ms = new MemoryStream())
            {
                AnalysisRegion.Save(ms, ImageFormat.Gif);
                ms.Position = 0;
                newbmp = (Bitmap)System.Drawing.Image.FromStream(ms);
            }



            ComplexImage complexImage = ComplexImage.FromBitmap(newbmp);
            
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



            return TotalSharpness / size;

            

            int getResizeSize(int width, int height)
            {
                int power = 15;
                while (Math.Pow(2, power) > width || Math.Pow(2, power) > height)
                {
                    power--;
                }
                return (int)Math.Pow(2, power);
            }
        }

        public double GetSharpness(ImageArea area, MagickImage fullImage)
        {
            MagickGeometry cropGeo = new MagickGeometry(area.X, area.Y, area.Width, area.Height);
            //need to change this.
            int smallSize = Math.Min(fullImage.Width, fullImage.Height) / 4;
            int size = getResizeSize(smallSize,smallSize);
            MagickGeometry resizeGeo = new MagickGeometry(size,size);
            resizeGeo.IgnoreAspectRatio = true;
            fullImage.Crop(cropGeo);
            fullImage.Resize(resizeGeo);

            //out to temp bitmap
            Bitmap bitmap = fullImage.ToBitmap();
            fullImage.Dispose();

            //convert to complex image.
            Bitmap bppBitmap;
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Gif);
                ms.Position = 0;
                bppBitmap = (Bitmap)System.Drawing.Image.FromStream(ms);
            }

            ComplexImage complexImage = ComplexImage.FromBitmap(bppBitmap);

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

            return TotalSharpness;


            int getResizeSize(int width, int height)
            {
                int power = 15;
                while (Math.Pow(2, power) > width || Math.Pow(2, power) > height)
                {
                    power--;
                }
                return (int)Math.Pow(2, power);
            }
        }

        public ImageArea getFaceSharpnessArea(Bitmap thumbnail, double ProcessingScale, int minFaceSize, int width, int height)
        {
            Accord.Vision.Detection.Cascades.FaceHaarCascade cascade = new Accord.Vision.Detection.Cascades.FaceHaarCascade();

            HaarObjectDetector detector = new HaarObjectDetector(cascade, minFaceSize, ObjectDetectorSearchMode.NoOverlap);

            Rectangle[] rectangles = detector.ProcessFrame(thumbnail);

            Rectangle biggestRect = new Rectangle();
            if(rectangles.Length == 0)
            {
                return new ImageArea(-1,-1,-1,-1);
            } 
            else
            {
                //program gives you x y length width. scale each by processingscale, find center, then submit a standardized length size.
                int largestSizeRect = 0;
                foreach (Rectangle rectangle in rectangles)
                {
                    //calculate size, set it to BiggestRect.
                    if(rectangle.Width * rectangle.Height > largestSizeRect) { largestSizeRect = rectangle.Width * rectangle.Height; biggestRect = rectangle; }
                }
                
                //scale it
                double multiplyfactor = (double)100 / ProcessingScale;
                double x = biggestRect.X * (multiplyfactor);
                double y = biggestRect.Y * multiplyfactor;
                double RectWidth = biggestRect.Width * multiplyfactor;
                double RectHeight = biggestRect.Height * multiplyfactor;

                //find center
                double centerX = x + RectWidth / 2;
                double centerY = y + RectHeight / 2;

                int SharpnessAreaSize = Math.Max(width, height) / 4;

                ImageArea area = new ImageArea((int)(centerX - SharpnessAreaSize / 2), (int)(centerY - SharpnessAreaSize / 2), (int)(SharpnessAreaSize), (int)(SharpnessAreaSize));


                return normalizeArea(area, width, height);
            }
        }
        public ImageArea getAutoSharpnessArea(List<SpeededUpRobustFeaturePoint> ImageFeatures, double ProcessingScale, int width, int height)
        {
            //finds average x and y feature point of the image and creates assigns ImageArea to a rectangle surrounding 1 standard deviation of the average.
            //List<double[]> observations = new List<double[]>();
            //for(int i = 0; i < ImageFeatures.Count; i++)
            //{
            //    List<double> list = new List<double>();
            //    list.Add(ImageFeatures[i].X);
            //    list.Add(ImageFeatures[i].Y);
            //    observations.Add(list.ToArray());
            //}
            //
            //KMeans kmeans = new KMeans(3);
            //KMeansClusterCollection clusters = kmeans.Learn(observations.ToArray());
            
            //Find cluster with the best result.
            
                        
            double sDevScale = 1.5;

            int SharpnessAreaSize = Math.Max(width, height) / 4;

            List<double> XCoords = new List<double>();
            List<double> YCoords = new List<double>();

            //Store X and Y into a new array
            //ImageFeatures are scaled. First multiply them by 1/ScalePercentage to normalize them to the resolution
            //of the Image.
            double scale = 1 / (ProcessingScale / 100);
            foreach (SpeededUpRobustFeaturePoint point in ImageFeatures)
            {
                XCoords.Add(point.X * scale);
                YCoords.Add((point.Y * scale));
            }

            int meanX = (int)Math.Floor(Measures.Mean(XCoords.ToArray()));
            int SDevX = (int)Math.Floor(Measures.StandardDeviation(XCoords.ToArray()));
            int meanY = (int)Math.Floor(Measures.Mean(YCoords.ToArray()));
            int SDevY = (int)Math.Floor(Measures.StandardDeviation(YCoords.ToArray()));

            //create image area


            //ImageArea area = new ImageArea((int)(meanX - sDevScale * (SDevX)), (int)(meanY - sDevScale * (SDevY)), (int)(sDevScale * SDevX * 2), (int)(sDevScale * SDevY * 2));
            ImageArea area = new ImageArea((int)(meanX - SharpnessAreaSize/2), (int)(meanY - SharpnessAreaSize/2), (int)(SharpnessAreaSize), (int)(SharpnessAreaSize));

            return normalizeArea(area,width,height);
            //Use Feature Points to get the most probably sharpness area range.
        }

        private ImageArea normalizeArea(ImageArea source, int ImageWidth, int ImageHeight)
        {
            //get upper/lower bounds

            int lowerX = source.X;
            int lowerY = source.Y;
            int upperX = source.X + source.Width;
            int upperY = source.Y + source.Height;

            //check if upper bounds exceeds width/height. If so, shift the imagearea;

            if(upperX > ImageWidth)
            {
                //shift the image back;
                lowerX -= (upperX - ImageWidth);
            }

            if(upperY > ImageHeight)
            {
                lowerY -= (upperY - ImageHeight);
            }

            //return the new imagearea, with the same width/height
            return new ImageArea(lowerX, lowerY, source.Width, source.Height);
        }

        public ImageArea getFullSharpnessArea(int width, int height)
        {
            return new ImageArea(0, 0, width, height);
        }
        public ImageArea getCenterSharpnessArea(int width, int height)
        {
            int X = (int)(width * 0.25);
            int Y = (int)(height * 0.25);
            int sendwidth = (int)(width * 0.5);
            int sendheight = (int)(height * 0.5);

            return new ImageArea(X, Y, sendwidth, sendheight);
        }

        public List<DoveEyeImageFileInformation> getImages(string root)
        {
            string[] files = Directory.GetFiles(root);
            List<FileInfo> ImageFileInfo = new List<FileInfo>();
            foreach (string filepath in files)
            {
                ImageFileInfo.Add(new FileInfo(filepath));
            }

            List<FileInfo> removablesQueue = new List<FileInfo>();

            //Eliminate file paths that aren't supported by ImageMagick
            foreach (FileInfo fileInfo in ImageFileInfo)
            {
                try
                {
                    //MagickImageInfo readTest = new MagickImageInfo(fileInfo.FullName); 
                    MagickImageInfo readTest = new MagickImageInfo(fileInfo.FullName);
                    if(readTest.Height == 0 || readTest.Width == 0) { removablesQueue.Add(fileInfo); }
                }
                catch
                {
                    //reading failed, delete the fileinfo
                    removablesQueue.Add(fileInfo);
                }
            }
            foreach (FileInfo info in removablesQueue)
            {
                ImageFileInfo.Remove(info);
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




        public class DoveEyeGroupingManager
        {
            //see constructor for notes.

            //TODO: This class should maybe be moved inside DoveEyeImageCanvas()

            /** ISSUE: Grouping management speed is slow. A multithreaded grouping analysis solution is not currently implemented, and would significantly improve
             * the speed of image processing.
             * 
             * 
             * multithreaded processing would look something like this:
             * 
             *  Constructor takes the list of images. Constructs a List<ComparisonState> for each of the images
             *  
             *  Comparison is divided amongst the mulitple threads.
             *  
             *  Once all comparisonstates are done... progress updater thread can recognize and set the full grouping list.
             */
            public readonly ImageComparisonType ComparisonType; //this is the comparison type used by the user.

            public List<DoveEyeContextualImage> Images;



            private ComparsionCheckWindow checkWindow;

            public class ImagePairComparisonState
            {
                //in the groupingmanager, a comparisoncheckwindow should be there, which is passed the list of imagepaircomparisonstates.
                //processor.compareimages should return a "prompt human" message in the doveeyeimagecomparisonresult
                //if prompt human message exists, update the comparisonstate.
                //humanpromptmanager thread should transfer the prompt human messages into the comparisoncheckwindow, which will be responsible
                //for sequentially doing each human prompt instead of the lame method which is used now.

                public DoveEyeContextualImage image1;
                public DoveEyeContextualImage image2;


                public ImageComparisonType comparisonType;
                public ComparisonState State;

                public DoveEyeImageComparisionResult result;

                public void AnalyzeImages()
                {
                    State = ComparisonState.AnalysisInProgress;
                    DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                    DoveEyeImageComparisionResult comparisonResult = processor.CompareImages(image1.Image, image2.Image, comparisonType); //need to edit this
                    result = comparisonResult;
                    switch(comparisonResult.result)
                    {
                        case DoveEyeImageComparisionResult.ComparisonResult.Similar:
                            State = ComparisonState.Similar;
                            break;
                        case DoveEyeImageComparisionResult.ComparisonResult.NotSimilar:
                            State = ComparisonState.NotSimilar;
                            break;
                        case DoveEyeImageComparisionResult.ComparisonResult.HumanPromptRequired:
                            State = ComparisonState.PendingUserRequest;
                            break;
                        case DoveEyeImageComparisionResult.ComparisonResult.Error:
                            throw new Exception();
                            break;
                        default:
                            throw new Exception();
                    }
                }

                public enum ComparisonState
                {
                    PendingAnalysis,
                    AnalysisInProgress,
                    PendingUserRequest,
                    Similar,
                    NotSimilar
                }
            }

            List<ImagePairComparisonState> comparisonStates = new List<ImagePairComparisonState>();

            public int TotalImages;
            public int Progress = 0;

            int ActiveThreads = 0;
            int MaxThreads = 10;

            public bool AnalysisComplete = false;

            public ObservableCollection<DoveEyeImageGroup> Groupings = new ObservableCollection<DoveEyeImageGroup>();

            public DoveEyeGroupingManager(ImageComparisonType cmpType, List<DoveEyeContextualImage> imgs, int MaxThreads)
            {
                ComparisonType = cmpType;
                Images = imgs;
                TotalImages = imgs.Count;
                this.MaxThreads = MaxThreads;

                for(int i = 0; i < imgs.Count - 1; i++)
                {
                    comparisonStates.Add(new ImagePairComparisonState() { image1 = imgs[i], image2 = imgs[i+1], State=ImagePairComparisonState.ComparisonState.PendingAnalysis, comparisonType=cmpType }) ;
                }

                //After assigning variables, user shoudl call BeginGroupAnalysis() to begin the analysis of images.
                //see BeginGroupAnalysis() for notes.
            }

            public void AnalysisManger()
            {
                //Code that starts a new thread to compare two images, limit up to 10 threads. Takes the first PendingAnalysis from the comparisonstate list.
                
                
                while(!AnalysisComplete)
                {
                    if (activeThreads() < MaxThreads)
                    {
                        for (int i = 0; i < comparisonStates.Count; i++)
                        {
                            if (comparisonStates[i].State == ImagePairComparisonState.ComparisonState.PendingAnalysis)
                            {
                                //Set it to in progress, begin the thread that analyzes the progress.
                                comparisonStates[i].State = ImagePairComparisonState.ComparisonState.AnalysisInProgress;
                                ActiveThreads++;
                                Thread AnalysisThread = new Thread(new ThreadStart(comparisonStates[i].AnalyzeImages));
                                AnalysisThread.SetApartmentState(ApartmentState.STA);
                                AnalysisThread.Start();
                                break;
                            }
                        }
                    }
                }

                //Code to execute when analysis is complete.
                int activeThreads()
                {
                    int active = 0;
                    foreach (ImagePairComparisonState item in comparisonStates)
                    {
                        if (item.State == ImagePairComparisonState.ComparisonState.AnalysisInProgress || item.State == ImagePairComparisonState.ComparisonState.PendingUserRequest)
                        {
                            active++;
                        }
                    }
                    return active;
                }

            }

            void AssignGroupings()
            {
                List<DoveEyeContextualImage> groupBuffer = new List<DoveEyeContextualImage>();

                groupBuffer.Add(Images[0]);

                for(int i = 0; i < comparisonStates.Count; i++)
                {
                    //check the outcome. if its good, then add to the buffer. If its bad, then split the group.
                    if (comparisonStates[i].State == ImagePairComparisonState.ComparisonState.Similar)
                    {
                        groupBuffer.Add(Images[i + 1]);
                    }
                    else if (comparisonStates[i].State == ImagePairComparisonState.ComparisonState.NotSimilar)
                    {
                        //split the group here.

                        //adding new group
                        Groupings.Add(new DoveEyeImageGroup() { Images = groupBuffer, GroupIndex = Groupings.Count, GroupName = Groupings.Count.ToString() });
                        
                        //reset group buffer and add current image.
                        groupBuffer = new List<DoveEyeContextualImage>();
                        groupBuffer.Add(Images[i + 1]);
                    } else
                    {
                        throw new Exception("This exception should never be called. Something has gone seriously wrong.");
                    }
                }

                //add the last group that isn't added in the previous for loop
                if(groupBuffer.Count!=0)
                {
                    Groupings.Add(new DoveEyeImageGroup() { Images = groupBuffer, GroupIndex = Groupings.Count, GroupName = Groupings.Count.ToString() });
                } else
                {
                    throw new Exception("Last group had no images, an event that should never happen");
                }

                //Should be done lol.
            }

            public void ProgressUpdater()
            {
                while(!AnalysisComplete)
                {
                    Progress = progress();

                    ActiveThreads = activeThreads();

                    if (Progress >= TotalImages) 
                    {
                        //analysis is complete. Get the groupings managed, and set the bool.
                        AssignGroupings();

                        AnalysisComplete = true;
                    }

                    Thread.Sleep(10);
                }

                int activeThreads()
                {
                    int active = 0;
                    foreach (ImagePairComparisonState item in comparisonStates)
                    {
                        if (item.State == ImagePairComparisonState.ComparisonState.AnalysisInProgress || item.State == ImagePairComparisonState.ComparisonState.PendingUserRequest)
                        {
                            active++;
                        }
                    }
                    return active;
                }

                int progress()
                {
                    int pending = 0;
                    foreach (ImagePairComparisonState item in comparisonStates)
                    {
                        if(item.State == ImagePairComparisonState.ComparisonState.PendingAnalysis || item.State == ImagePairComparisonState.ComparisonState.AnalysisInProgress || item.State == ImagePairComparisonState.ComparisonState.PendingUserRequest)
                        {
                            pending++;
                        }
                    }
                    return TotalImages - pending;
                }
            }
            void ManageHumanPrompter()
            {
                checkWindow = new ComparsionCheckWindow(ref comparisonStates);

                
                if(!checkWindow.AllImagesPrompted) { checkWindow.Show(); }
                
                
                System.Windows.Threading.Dispatcher.Run();
                checkWindow.PromptNext();
            }
            public void BeginGroupAnalysis()
            {
                //This method will initialize two threads - one analysismanager, one progressupdater. Analysismanager can distribute the list in a multithreaded workload.

                //This method begins the process of finding groups.
                //To find groups based on this approach, it enumerates and compares every two images using the comparisontype specified in the constructor


                //Make new threads

                Thread GroupAnalysisManagerThread = new Thread(new ThreadStart(AnalysisManger));
                GroupAnalysisManagerThread.Start();

                Thread ProgressUpdaterThread = new Thread(new ThreadStart(ProgressUpdater));
                ProgressUpdaterThread.Start();

                Thread HumanPrompterManagerThread = new Thread(new ThreadStart(ManageHumanPrompter));
                HumanPrompterManagerThread.SetApartmentState(ApartmentState.STA);
                
                HumanPrompterManagerThread.Start(); 
                
                //OLD CODE - NOT MULITHREADED
                //List<DoveEyeContextualImage> TemporaryBuffer = new List<DoveEyeContextualImage>();
                //for (int i = 0; i < Images.Count - 1; i++)
                //{
                //    //enumerating through images.
                //    DoveEyeImageProcessor processor = new DoveEyeImageProcessor();
                //    DoveEyeImageComparisionResult comparisonResult = processor.CompareImages(Images[i].Image, Images[i + 1].Image, ComparisonType);
                //
                //    if (comparisonResult.Similar)
                //    {
                //        TemporaryBuffer.Add(Images[i]);
                //        //add to current grouping.
                //    }
                //    else
                //    {
                //        //Split group - create a new group starting from this image.
                //        TemporaryBuffer.Add(Images[i]);
                //        DoveEyeImageGroup ImageGroup = new DoveEyeImageGroup();
                //        ImageGroup.Images = TemporaryBuffer;
                //        TemporaryBuffer = new List<DoveEyeContextualImage>();
                //        ImageGroup.GroupIndex = Groupings.Count();
                //
                //        Groupings.Add(ImageGroup);
                //    }
                //
                //    Progress++;
                //}
                //AnalysisComplete = true;

                //Once the analysis is complete, user should migrate the data within this.Groupings to the groupings section of the Image Canvas.
            }
        }

        public class DoveEyeAnalysisManager
        {
            //This class is critical to efficient grouping detection.
            
            //TODO: This class should maybe be moved inside of DoveEyeImageCanvas();

            /**After constructing the class, the user shoudl call BeginAnalysis() and wait for this.AnalysisComplete to be true.
             * The analysis process is both memory, CPU, and hard-disk intensive. A powerful cpu assists in the analysis process.
             * 
             * See BeginAnalysis() for documentation.
             */

            /**CRITICAL ISSUE:
             * This analysis uses the ImageMagick library to open images, convert them to Bitmap, and perform some processing. 
             * ImageMagick is used to maximize the number of supported file formats, including many RAW formats.
             * 
             * The issue:
             * ImageMagick begins writing its analysis files to a temporary page file after reaching a (seemingly) arbitrary memory limit.
             * Once it begins writing to a page file, EXTREME hard drive overhead slows down the entire process to a crawl, multiplying the processing time
             * by 5-6 times or more.
             * 
             * It appears that more available system memory helps with this issue significantly, and this program is complied for 64-bit systems.
             * 
             * To circumvent this, the best method seems to be by artificially limiting the processing speed by setting the number of threads to roughly 2/3 
             * of the system's total threads. In my case, this means setting the processing threads to 8 instead of 12.
             * 
             * This is a very shady solution, and the ImageMagick library doesn't seem to have human-understandable documentation.
             * Perhaps the best solution will be to implement a progress speed detection system, and dynamically lower the number of threads of throttling 
             * is detected.
             * 
             * For now, it appears using 8 threads works well.
             */
            

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

            double ScalePercentageForThumbnails; //NOTE: THIS IS AN INTEGER CORRESPONDING TO THE PERCENTAGE, NOT a decimal (20 vs 0.2 for 20%)

            public bool AnalysisComplete;

            int ThreadCount;
            int QueueBuffer;
            DoveEyeImage.SharpnessAreatype sharpnessAreaType;

            public DoveEyeAnalysisManager(string rootfile, int threads, int queuecount, DoveEyeImage.SharpnessAreatype sharpnessAreatype, double ScalePercentageForThumb)
            {
                root = rootfile;
                sharpnessAreaType = sharpnessAreatype;
                ThreadCount = threads;
                QueueBuffer = queuecount;
                ScalePercentageForThumbnails = ScalePercentageForThumb;
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
                /** Analyzing images is a complex process. Analyzing each image, one after the other (sequentially) will be slow, as it is limited to
                 * using just one CPU thread.
                 * 
                 * The logical solution is to distribute the images across a multithreaded system. Perhaps 10 threads are analyzing images simultaneously,
                 * dramatically increasing CPU utilization.
                 * 
                 * However, doing this does not work either, because each analysis thread will request a hard drive read.
                 * If 10 analysis threads simultaneously request a hard drive read for files in the same hard disk, the hard drive will almost certainly
                 * skip around, say reading image 1, then 8, then 7, the 4, then 10, and so on.
                 * 
                 * This scattered hard drive read request incurs massive disk overhead, and severely limits read speeds to the point where it impacts system
                 * performance. (In my testing, it limits my 5200rpm SATA hdd to about <6mbps, as opposed to the 60mbps it is capable of.
                 * 
                 * To circumvent this issue, analysis is divided into two components:
                 * One thread - FileManagerThread() - is responsible for sequentially reading images from the hard drive.
                 * Another thread - AnalysisManagerThread() - is responsible for distributing these read images to the various analysis threads.
                 * Another thread - MemoryManagerThread() - is responsible for Disposeing files that have already been analyzed to save memory.
                 * The final thread - ProgressUpdaterThread() - checks the analysis progress and updates the relevant variables.
                 * 
                 * This solution works well - all system resources are utilized maximally, aside from the odd imagemagick issue.
                 * 
                 * The user should wait for this.AnalysisComplete to be true before continuing the program. 
                 */

                ResourceLimits.LimitMemory(new Percentage(1.00));



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

                                Images.Add(new DoveEyeContextualImage(ImageFileInformation[i], FileStreams[i], sharpnessAreaType, ScalePercentageForThumbnails));
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