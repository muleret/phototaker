using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Provider;
using Android.Net;
using Java.IO;
using System;
using Android.Graphics;
using Android.Content.PM;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net;
using Microsoft.WindowsAzure.Storage.Queue;

namespace PhotoTaker.Droid
{
	[Activity (Label = "PhotoTaker.Android", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
        ImageView _imageView;
        TextView _message1;

        int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            if (IsThereAnAppToTakePictures())
            {
                CreateDirectoryForPictures();

                Button button = FindViewById<Button>(Resource.Id.myButton);
                _imageView = FindViewById<ImageView>(Resource.Id.imageView1);
                button.Click += TakeAPicture;
            }

            _message1 = FindViewById<TextView>(Resource.Id.message1);
        }

        private void CreateDirectoryForPictures()
        {
            App._dir = new File(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryPictures), "CameraAppDemo");
            if (!App._dir.Exists())
            {
                App._dir.Mkdirs();
            }
        }

        private bool IsThereAnAppToTakePictures()
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            IList<ResolveInfo> availableActivities =
                PackageManager.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
            return availableActivities != null && availableActivities.Count > 0;
        }

        private void TakeAPicture(object sender, EventArgs eventArgs)
        {
            Intent intent = new Intent(MediaStore.ActionImageCapture);
            App._file = new File(App._dir, String.Format("myPhoto_{0}.jpg", Guid.NewGuid()));
            intent.PutExtra(MediaStore.ExtraOutput, Android.Net.Uri.FromFile(App._file));
            StartActivityForResult(intent, 0);
        }

        protected async override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (resultCode == Result.Canceled)
                return;

            _message1.Text = "Uploading...";

            // Make it available in the gallery

            try
            {
                Intent mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile);
                Android.Net.Uri contentUri = Android.Net.Uri.FromFile(App._file);
                mediaScanIntent.SetData(contentUri);
                SendBroadcast(mediaScanIntent);

                // TODO upload through Web API instead
                //HttpWebRequest uploadRequrest = (HttpWebRequest)HttpWebRequest.Create("photosharing.azurewebsites.net/api/upload");

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=;AccountKey=;EndpointSuffix=core.windows.net");
                var fileName = App._file.Name;

                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference("images1");

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);

                // Create or overwrite the "myblob" blob with contents from a local file.
                await blockBlob.UploadFromFileAsync(App._file.Path);

                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                var tnQueue = queueClient.GetQueueReference("thumbnailqueue");
                await tnQueue.AddMessageAsync(new CloudQueueMessage(blockBlob.StorageUri.PrimaryUri.AbsoluteUri));

                // Display in ImageView. We will resize the bitmap to fit the display.
                // Loading the full sized image will consume to much memory
                // and cause the application to crash.

                int height = Resources.DisplayMetrics.HeightPixels;
                int width = _imageView.Height;

                // To preview the original image: App.bitmap = BitmapFactory.DecodeFile(App._file.Path); 
                App.bitmap = App._file.Path.LoadAndResizeBitmap(width, height);

                if (App.bitmap != null)
                {
                    _imageView.SetImageBitmap(App.bitmap);
                    App.bitmap = null;
                }

                _message1.Text = "Uploaded!";
            }
            catch (Exception ex)
            {
                _message1.Text = "Error: " + ex.Message;
            }

            // Dispose of the Java side bitmap.
            GC.Collect();
        }
    }

    public static class App
    {
        public static File _file;
        public static File _dir;
        public static Bitmap bitmap;
    }

    public static class BitmapHelpers
    {
        public static Bitmap LoadAndResizeBitmap(this string fileName, int width, int height)
        {
            // First we get the the dimensions of the file on disk
            BitmapFactory.Options options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(fileName, options);

            // Next we calculate the ratio that we need to resize the image by
            // in order to fit the requested dimensions.
            int outHeight = options.OutHeight;
            int outWidth = options.OutWidth;
            int inSampleSize = 1;

            if (outHeight > height || outWidth > width)
            {
                inSampleSize = outWidth > outHeight
                                   ? outHeight / height
                                   : outWidth / width;
            }

            // Now we will load the image and have BitmapFactory resize it for us.
            options.InSampleSize = inSampleSize;
            options.InJustDecodeBounds = false;
            Bitmap resizedBitmap = BitmapFactory.DecodeFile(fileName, options);

            return resizedBitmap;
        }
    }
}


