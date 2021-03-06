﻿using PicsDirectoryDisplayWin.lib;
using PicsDirectoryDisplayWin.lib_ImgIO;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PicsDirectoryDisplayWin
{
    /// <summary>
    /// this class shows 2 pages, 1 is gallery shown after images are 
    /// searched from mobile, 2 is selected images which is a subset of all images available
    /// </summary>
    public partial class SimpleGallery : Form
    {

        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public List<TheImage> AllImages { get; set; }
        public Form WifiConnectHelpObject { get; set; }
        public Form AnimationFormObject { get; set; }
        public List<string> SelectedImageKeys { get; set; }

        
        public string USBDriveLetter;
        public bool FilesChanged
        {
            set
            {
                if (fileChangedNotifer != null)
                    fileChangedNotifer(this, new EventArgs());
            }
        }

        public bool RefreshGalleryNotify
        {
            set
            {
                if (fileChangedNotifer != null)
                    refreshGalleryNotifier(this, new EventArgs());
            }
        }

        private bool IS_USBConnection = false;
        private int foundImageCount = 0;
        private Timer timer;
        //TODO : Remove this timer, make a queue to process multiple requests, process only, first and last request.
        private int RefreshResponseDelay = 1000; //milisec
        //private string WebSiteSearchDir = @"C:\inetpub\wwwroot\ps\Uploads\030357B624D9";
        private Waiter waiter = new Waiter();
        private bool SelectionChanged = false;
        private readonly ImageIO imageIO = new ImageIO();
        //gallery preview listview
        private ListView galleryPreview;
        private UI.Print print;
        private bool OnGalleryPreviewPage = false;
        private readonly string CheckSymbol;
        private readonly Color SelectedColor = Color.Silver;
        private readonly Font SelectedFont = new Font(new Font("Arial", 10.0f), FontStyle.Bold);
        private readonly Font UnSelectedFont = new Font(new Font("Arial", 8.0f), FontStyle.Regular);
       // private int fileChangedCounter = 0;
        private event EventHandler fileChangedNotifer;
        private event EventHandler refreshGalleryNotifier;
     
        public SimpleGallery(bool isUSBConnection = false)
        {
            InitializeComponent();
            fileChangedNotifer += SimpleGallery_fileChangedNotifer;
            refreshGalleryNotifier += SimpleGallery_refreshGalleryNotifier;
            imglist.MultiSelect = false;
            //imglist.View = View.LargeIcon;
            imglist.LabelWrap = true;
            imglist.Font = UnSelectedFont;
            imglist.ItemSelectionChanged += Imglist_ItemSelectionChanged;
            imglist.Click += Imglist_Click;
            SelectedImageKeys = new List<string>();
            //UploadButton.Visible = IS_USBConnection;
            string checkUnicode = "2714"; // ballot box -1F5F9
            int value = int.Parse(checkUnicode, System.Globalization.NumberStyles.HexNumber);
            CheckSymbol = char.ConvertFromUtf32(value).ToString();

            UploadButton.Text = ConfigurationManager.AppSettings["UploadButton"];
            btn_Next.Text = ConfigurationManager.AppSettings["NextButton"];
            btn_Back.Text = ConfigurationManager.AppSettings["BackButton"];
            label6.Text = ConfigurationManager.AppSettings["BillInfo"];
            label12.Text = ConfigurationManager.AppSettings["PrintSizeText"];
            label11.Text = Globals.PrintSelection.ToString(); //ConfigurationManager.AppSettings["PrintSizeValue"];
            label4.Text = ConfigurationManager.AppSettings["CostText"] ;
            label8.Text = ConfigurationManager.AppSettings["CostValue"+ Globals.PrintSelection.ToString()]; 
            label5.Text = ConfigurationManager.AppSettings["NoOfPicsText"];
            label_PicsCount.Text = ConfigurationManager.AppSettings["NoOfPicsInitialValue"];
            label10.Text = ConfigurationManager.AppSettings["AmountText"];
            label9.Text = ConfigurationManager.AppSettings["AmountInitialValue"];
            label2.Text = ConfigurationManager.AppSettings["GSTText"];
            label1.Text = ConfigurationManager.AppSettings["GSTValue"];
            label14.Text = ConfigurationManager.AppSettings["TotalText"];
            label13.Text = ConfigurationManager.AppSettings["TotalValue"];
            warningTxt.Text = ConfigurationManager.AppSettings["WarningText"];
            IS_USBConnection = isUSBConnection;
            UploadButton.Visible = IS_USBConnection;
            //tb.BackgroundImage = GlobalImageCache.TableBgImg;
            tb.BackColor = Color.FromName(ConfigurationManager.AppSettings["AppBackgndColor"]);
            tb.BackgroundImageLayout = ImageLayout.Stretch;

            if (ConfigurationManager.AppSettings["Mode"] != "Diagnostic")
            {
                //fullscreen
                this.TopMost = true;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void SimpleGallery_refreshGalleryNotifier(object sender, EventArgs e)
        {
            if (AllImages.Count>0)
            {
                ShowGallerySelectionImages(AllImages[0]);
            }
            
        }

        private void SimpleGallery_fileChangedNotifer(object sender, EventArgs e)
        {
            if (InvokeRequired && System.Diagnostics.Debugger.IsAttached)
            {
                MessageBox.Show("file watcher, Invoke needed");
            }
            AllImages = new List<TheImage>();
            waiter = new Waiter();
            imageIO.Wifi_CheckForImages(AllImages, InvokeRequired, ConfigurationManager.AppSettings["WebSiteSearchDir"],
                this, waiter, ReportProgress, Done);
        }

        private void PrepareFormForGalleryPreview()
        {
            UploadButton.Enabled = false;
            btn_Next.Text = "Done";
            OnGalleryPreviewPage = true;
            //folder_list.Visible = false;
            imglist.Visible = false;
            galleryPreview = new ListView();
            galleryPreview.Dock = DockStyle.Fill;
            tb.Controls.Add(galleryPreview,1,0);
            galleryPreview.Enabled = false;
        }


        private void PrepareFormForGallerySelection()
        {
            UploadButton.Enabled = true;
            btn_Next.Text = "Next";
            OnGalleryPreviewPage = false;
            //folder_list.Visible = true;
            imglist.Visible = true;
            galleryPreview.Visible = false;
          
        }

        private void Imglist_Click(object sender, EventArgs e)
        {
            if (!SelectionChanged)
            {
                ListViewItem item = imglist.SelectedItems[0];
                if (item != null)
                    UnSelectImage(item);
            }
            SelectionChanged = false;
        
        }

        private void Imglist_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            SelectionChanged = true;
            if (e.IsSelected)
            {
                if (!SelectedImageKeys.Contains((e.Item).ImageKey))
                {
                    SelectImage(e.Item);
                }
                else if (SelectedImageKeys.Contains(e.Item.ImageKey))
                {
                    UnSelectImage(e.Item);
                }
            }

        }

        private void UnSelectImage(ListViewItem item)
        {
            if (item.Checked)
            {
                SelectedImageKeys.Remove(item.ImageKey);

                item.Checked = false;
                item.BackColor = Color.White;
                item.Focused = false;
                //string copyrightUnicode = "2714"; // ballot box -1F5F9
                //int value = int.Parse(copyrightUnicode, System.Globalization.NumberStyles.HexNumber);
                //string symbol = char.ConvertFromUtf32(value).ToString();
                item.Font = UnSelectedFont;
                item.Text = item.Text.Replace("[" + CheckSymbol + "] ", "");
                UpdateBillDetails(SelectedImageKeys.Count);
            }
           

        }

        private void SelectImage(ListViewItem item)
        {
            if (item.Checked ==false)
            {
                SelectedImageKeys.Add(item.ImageKey);
                SelectedImageChecked(item);
                UpdateBillDetails(SelectedImageKeys.Count);
            }

        }

        private void SelectedImageChecked(ListViewItem item)
        {
            item.Checked = true;
            item.BackColor = SelectedColor;
            item.Focused = true;
            item.Font = SelectedFont;
            item.Text = "[" + CheckSymbol + "] " + item.Text;
        }

        private void SimpleGallery_Load(object sender, EventArgs e)
        {

            //TODO : fix, below line, all images [1] is wrong, it shud only detect images and not go in subdirectory
            if (AllImages != null && AllImages.Count > 0)
                ShowGallerySelectionImages(AllImages[0]);

            timer = new Timer();
            timer.Interval = 3000;
            timer.Tick += Timer_Tick;
            timer.Start();

        }

        /// <summary>
        /// This timer tick event is very important, as it keep watch over folder content, keep up pics if 
        /// some non jop image is uploaded, it also triggers image load, shows progress bar, enables/disable
        /// button etc.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
           int FilesInWebSearchDir = new DirectoryInfo(ConfigurationManager.AppSettings["WebSiteSearchDir"]).EnumerateFiles().Count();
           int FilesInThumbsDir = new DirectoryInfo(ConfigurationManager.AppSettings["WebSiteSearchDir"] + "\\thumbs").EnumerateFiles().Count();
            bool isloading = false;

            //This will delete non jpg files, then image count should b equal to thumbs count and 
            // progress bar should get stopped.
            DeleteNonImageFiles();


            if (AllImages != null && AllImages.Count>0 &&
                AllImages[0].PeerImages.Count != FilesInWebSearchDir && AllImages[0].PeerImages.Count < Globals.IncludeMaxImages)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => { FilesChanged = true; }));
                    isloading = true;
                }
                else
                {
                    FilesChanged = true;
                    isloading = true;
                }
                return;
            }

            if (imglist.LargeImageList != null && imglist.Items.Count != imglist.LargeImageList.Images.Count)
            {
                RefreshGalleryNotify = true;
                isloading = true;
            }

            if (FilesInWebSearchDir != FilesInThumbsDir && FilesInThumbsDir < Globals.IncludeMaxImages)
            {
                
                RefreshThumbnails();
                isloading = true;
            }

            if (IS_USBConnection && FilesInWebSearchDir != FilesInThumbsDir && FilesInThumbsDir < Globals.IncludeMaxImages)
            {
                FilesChanged = true;
                isloading = true;
                RefreshThumbnails();
                
            }

            btn_Next.Enabled = !isloading;
            imglist.Enabled = !isloading;
            UploadButton.Enabled = !isloading;
            loadingImageslabel.Visible = isloading;
            LoadingImagesPBar.Visible = isloading;

         

            ValidateSelectedImages();

            UpdateBillDetails(SelectedImageKeys.Count);
        }

        private void DeleteNonImageFiles()
        {
            imageIO.DeleteAllNonImageFilesInDrectory(ConfigurationManager.AppSettings["WebSiteSearchDir"]);

        }

        private void ValidateSelectedImages()
        {
            //This condition will be true only when no image is selected, i.e first time upload.
            if (SelectedImageKeys.Count == 0)
            {
                for (int i = 0; i < imglist.Items.Count; i++)
                {
                    SelectImage(imglist.Items[i]);
                }
            }
            //Is selected image also marked with check.
            for (int i = 0; i < imglist.Items.Count; i++)
            {
                if(SelectedImageKeys.Contains(imglist.Items[i].ImageKey) && 
                    !imglist.Items[i].Text.Contains("[" + CheckSymbol + "] "))
                {
                    SelectedImageChecked(imglist.Items[i]);
                }
            }

        }


   


    private void UpdateBillDetails(int count)
        {
            //For passport, image repeats and count is fixed for a page
            if (Globals.PrintSelection == Globals.PrintSize.Postcard)
                count = Globals.PostcardImageCountInAPage;
            if (Globals.PrintSelection == Globals.PrintSize.Passport)
                count = Globals.PassportImageCountInAPage;

            if (!string.IsNullOrEmpty(label8.Text) &&
                !string.IsNullOrEmpty(label1.Text) )
            {
           
            label_PicsCount.Text = count.ToString();
            label9.Text = (Convert.ToInt16(label8.Text) * count).ToString();
            label13.Text = (((Convert.ToInt16(label8.Text) * count) * Convert.ToInt16(label1.Text) / 100)
                            + (Convert.ToInt16(label8.Text) * count)).ToString();
            }
        }


        /// <summary>
        /// Shows fianll selection, gallery preview
        /// </summary>
        /// <param name="imageKeys"></param>
        private void ShowSelectedImages(List<string> imageKeys)
        {
            previewImages.Images.Clear();
            if (galleryPreview.LargeImageList != null && galleryPreview.LargeImageList.Images.Count>0)
                galleryPreview.LargeImageList.Images.Clear();
            galleryPreview.Clear(); //galleryPreview.LargeImageList.Images.Clear();
            foreach (var item in imageKeys)
            {
               
                string[] imgDetails = item.Split('|');
                //Get thumbnail
                string tempImg = imgDetails[0].Replace(imgDetails[1], "thumbs/") + Path.GetFileNameWithoutExtension(imgDetails[1]) + ".jpg";

                //string imgName = item.Split('|')[1];
                previewImages.Images.Add(tempImg, imageIO.GetImage(tempImg));
                //previewImages.Images.Add(tempImg, Image.FromFile(tempImg));
                //TODO    "thumbnail size 80,80 should be in a config file."
                previewImages.ImageSize = new Size(80, 80);
                galleryPreview.LargeImageList = previewImages;
                // image key is the image sleected from imagelist collection, key must present in imagelist above\
                galleryPreview.Items.Add(imgDetails[1], tempImg);
                galleryPreview.Show();
            }
            
        }

        



        private void ShowGallerySelectionImages(TheImage obj)
        {

            if (imglist.LargeImageList != null && imglist.LargeImageList.Images.Count > 0)
            {
                imglist.LargeImageList.Images.Clear();
            }
            SelectedImageKeys.Clear();
            imglist.Clear();//imglist.LargeImageList.Images.Clear();
            imageIO.CreateImageListFromThumbnails(obj, imgs);
            imglist.LargeImageList = imgs;
            CheckForMaxImageWarning();

            foreach (var item in obj.PeerImages)
            {
                //SelectImage(item);
                // image key is the image sleected from imagelist collection, key must present in imagelist above
                //var lvitem = new ListViewItem(item.ImageName, item.ImageKey);
                //SelectedImageChecked(lvitem);
                imglist.Items.Add(item.ImageName, item.ImageKey);

            }

            foreach (ListViewItem item in imglist.Items)
            {
                SelectImage(item);
            }

            imglist.Show();

        }

        private void CheckForMaxImageWarning()
        {
            if (AllImages.Count != 0 && AllImages[0].PeerImages.Count == Globals.IncludeMaxImages)
            {
                warningTxt.Text = "Max Image count:" + Globals.IncludeMaxImages + ".";
                pictureBox4.Visible = true;
            }
            else if (AllImages.Count != 0 && AllImages[0].PeerImages.Count < Globals.IncludeMaxImages)
            {
                warningTxt.Text = "";
                pictureBox4.Visible = false;
            }
        }



        /// <summary>
        /// Shows sleected images, first convert images to a collection. then remove extra controls from form and'
        /// show only images selcted.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Next_Click(object sender, EventArgs e)
        {
            if (ConfigurationManager.AppSettings["Mode"] == "Diagnostic")
                logger.Log(NLog.LogLevel.Info, "Inside Next button click function.");

            if (SelectedImageKeys.Count > 0)
            {
                //gallery preview is page where all final pics are shown before print
                if (!OnGalleryPreviewPage)
                {
                    PrepareFormForGalleryPreview();
                    ShowSelectedImages(SelectedImageKeys);
                }
                else
                {
                    PrepareForPrinting();
                }
            }
            else
            {
                //TODO: show error, no image selected
            }
        }

        private void PrepareForPrinting()
        {
            //this.Visible = false;
            timer.Stop();
            timer.Dispose();
            this.Close();
            print = new UI.Print(this, WifiConnectHelpObject, AnimationFormObject, waiter, SelectedImageKeys.Count);
            //print.SelectedImages = imgs;
            print.SelectedImages = SelectedImageKeys;
            print.ShowDialog();

        }

        private void btn_Back_Click(object sender, EventArgs e)
        {
           // Fix this

            if (SelectedImageKeys.Count > 0)
            {
                //gallery preview is page where all final pics are shown before print
                if (!OnGalleryPreviewPage)
                {
                    //PrepareFormForGallerySelection();
                    BackTOAnimation();
                }
                else
                {
                    PrepareFormForGallerySelection();
                    ///PrepareFormForGalleryPreview();
                    //ShowSelectedImages(SelectedImageKeys);
                }
            }
            else
            {
                //TODO: show error, no image selected
                BackTOAnimation();
            }

            
        }

        private void BackTOAnimation()
        {
            Application.Exit();
           
        }

        private void WebSiteUploadsWatcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            
        
        }

        /// <summary>
        /// This method is called after images are searched inside directory, its a call back method
        /// </summary>
        /// <param name="IsWeb"></param>
        private void Done(bool IsWeb)
        {


            foreach (var item in AllImages)
            {
                //Create Thumbnails
                Task task = new Task(async () =>
                {
                    await imageIO.DirectConn_CreateThumbnails(item);
                });
                task.Start();
                if (InvokeRequired)
                {
                    Invoke(new Action(() => ReportProgressForThumbnails(item.ImageDirName)));
                }
            }

           

            if (InvokeRequired)
            {
                Invoke(new Action(() => { waiter.Close(); waiter.Dispose(); }));
                Invoke(new Action(() => RefreshGalleryNotify = true));
                //if (fileChangedCounter > 1)
                //{// again raise event.
                //    Invoke(new Action(() => FilesChanged = true));
                //}
                //Invoke(new Action(() => fileChangedCounter = 0));
                Invoke(new Action(() => CheckForMaxImageWarning()));
                
            }
            else
            {
                if (waiter!=null)
                {
                    waiter.Close(); waiter.Dispose();
                }
               
                RefreshGalleryNotify = true;
                CheckForMaxImageWarning();
              
            }
            
        }

        private void ReportProgressForThumbnails(string dirName)
        {
            //foundImageCount = (foundImageCount + obj.ImageDirTotalImages);
            waiter.FileFoundLabelText = "Creating image thumbnails for : " + dirName;
            //AllImages.Add(obj);
        }

        private void ReportProgress(TheImage obj)
        {
            foundImageCount = (foundImageCount + obj.ImageDirTotalImages);
            //TODO : write here invoke required and invoke to display images found count on form
            //if(InvokeRequired)
            //    Invoke(new Action(() => label13.Text = foundImageCount.ToString() + " images found"));
            AllImages.Add(obj);
        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void RefreshThumbnails()
        {
            if (AllImages==null || AllImages.Count==0)
                return;
            //Create Thumbnails
            Task task = new Task(async () =>
            {
                await imageIO.DirectConn_CreateThumbnails(AllImages[0]);
            });
            task.Start();
            if (InvokeRequired)
            {
                Invoke(new Action(() => ReportProgressForThumbnails(AllImages[0].ImageDirName)));
            }
            RefreshGalleryNotify = true;
        }

        private void UploadButton_Click(object sender, EventArgs e)
        {
            imglist.Enabled = false;
            UploadUSBFilesDialog.InitialDirectory = USBDriveLetter;
            UploadUSBFilesDialog.ShowDialog();
            UploadUSBFilesDialog.DefaultExt = ".jpg";
            UploadUSBFilesDialog.Multiselect = true;
            bool pass = true;
             pass =   UploadUSBFilesDialog.SafeFileNames.Any((x) => {
                 if (x.ToLower().Contains(".jpg"))
                     // || x.ToLower().Contains(".jpeg"))
                     pass = true;
                 else
                     pass = false;
                    return pass;
                });
            if (pass)
            {
                for (int i = 0; i < UploadUSBFilesDialog.FileNames.Count(); i++)
                {
                    if (!File.Exists(ConfigurationManager.AppSettings["WebSiteSearchDir"] + "\\" +
                        UploadUSBFilesDialog.SafeFileNames[i]))
                    {
                        File.Copy(UploadUSBFilesDialog.FileNames[i], ConfigurationManager.AppSettings["WebSiteSearchDir"] + "\\" +
                        UploadUSBFilesDialog.SafeFileNames[i]);
                    }
                    
                }
            }
            else
            {
                MessageBox.Show("Only image (jpg) is allowed");
            }
            imglist.Enabled = true ;

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Maximized;
                this.FormBorderStyle = FormBorderStyle.None; this.ControlBox = false;
                return;
            }

            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.ControlBox = true;
                return;
            }
        }
    }


}
