using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading;
using DPUruNet;
using Newtonsoft.Json;

namespace WPFHuella5100FingerPrint
{
    public class ReadFingerPrint
    {
        private bool reset { get; set; }

        private Reader currentReader { get; set; }

        private HttpClient client;

        //
        private string basseAddress;

        //
        private HttpResponseMessage response;

        //
        private ResponseMatch result;

        public Action<bool> isCapture;

        public Action<bool> isMatch;

        public Action<string> callbackError;

        private int identification;

        public ReadFingerPrint(int identification)
        {
            basseAddress = "http://200.122.221.193:8830/Api/";
            this.identification = identification;
            client = new HttpClient();
            client.BaseAddress = new Uri(basseAddress);
            currentReader = ReaderCollection.GetReaders().FirstOrDefault();
        }

        public void star()
        {
            // Reset variables
            if (OpenReader())
            {
                if (!StartCaptureAsync(this.OnCaptured))
                {
                }
            }
        }

        public void stop()
        {
            CancelCaptureAndCloseReader(OnCaptured);
        }

        public bool OpenReader()
        {
            reset = false;
            Constants.ResultCode result = Constants.ResultCode.DP_DEVICE_FAILURE;

            // Open reader
            result = currentReader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);

            if (result != Constants.ResultCode.DP_SUCCESS)
            {
                reset = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Hookup capture handler and start capture.
        /// </summary>
        /// <param name="OnCaptured">Delegate to hookup as handler of the On_Captured event</param>
        /// <returns>Returns true if successful; false if unsuccessful</returns>
        public bool StartCaptureAsync(Reader.CaptureCallback OnCaptured)
        {
            // Activate capture handler
            currentReader.On_Captured += new Reader.CaptureCallback(OnCaptured);

            // Call capture
            if (!CaptureFingerAsync())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cancel the capture and then close the reader.
        /// </summary>
        /// <param name="OnCaptured">Delegate to unhook as handler of the On_Captured event </param>
        public void CancelCaptureAndCloseReader(Reader.CaptureCallback OnCaptured)
        {
            if (currentReader != null)
            {
                currentReader.CancelCapture();

                // Dispose of reader handle and unhook reader events.
                currentReader.Dispose();

                if (reset)
                {
                    currentReader = null;
                }
            }
        }

        /// <summary>
        /// Check the device status before starting capture.
        /// </summary>
        /// <returns></returns>
        public void GetStatus()
        {
            Constants.ResultCode result = currentReader.GetStatus();

            if ((result != Constants.ResultCode.DP_SUCCESS))
            {
                if (currentReader != null)
                {
                    currentReader.Dispose();
                    currentReader = null;
                }
                throw new Exception("" + result);
            }

            if ((currentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY))
            {
                Thread.Sleep(50);
            }
            else if ((currentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION))
            {
                currentReader.Calibrate();
            }
            else if ((currentReader.Status.Status != Constants.ReaderStatuses.DP_STATUS_READY))
            {
                throw new Exception("Reader Status - " + currentReader.Status.Status);
            }
        }

        /// <summary>
        /// Check quality of the resulting capture.
        /// </summary>
        public bool CheckCaptureResult(CaptureResult captureResult)
        {
            if (captureResult.Data == null)
            {
                if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    reset = true;
                    throw new Exception(captureResult.ResultCode.ToString());
                }

                // Send message if quality shows fake finger
                if ((captureResult.Quality != Constants.CaptureQuality.DP_QUALITY_CANCELED))
                {
                    throw new Exception("Quality - " + captureResult.Quality);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Function to capture a finger. Always get status first and calibrate or wait if necessary.  Always check status and capture errors.
        /// </summary>
        /// <param name="fid"></param>
        /// <returns></returns>
        public bool CaptureFingerAsync()
        {
            try
            {
                GetStatus();

                Constants.ResultCode captureResult = currentReader.CaptureAsync(Constants.Formats.Fid.ANSI, Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT, currentReader.Capabilities.Resolutions[0]);
                if (captureResult != Constants.ResultCode.DP_SUCCESS)
                {
                    reset = true;
                    throw new Exception("" + captureResult);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Create a bitmap from raw data in row/column format.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap CreateBitmap(byte[] bytes, int width, int height)
        {
            byte[] rgbBytes = new byte[bytes.Length * 3];

            for (int i = 0; i <= bytes.Length - 1; i++)
            {
                rgbBytes[(i * 3)] = bytes[i];
                rgbBytes[(i * 3) + 1] = bytes[i];
                rgbBytes[(i * 3) + 2] = bytes[i];
            }
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            for (int i = 0; i <= bmp.Height - 1; i++)
            {
                IntPtr p = new IntPtr(data.Scan0.ToInt64() + data.Stride * i);
                System.Runtime.InteropServices.Marshal.Copy(rgbBytes, i * bmp.Width * 3, p, bmp.Width * 3);
            }

            bmp.UnlockBits(data);

            return bmp;
        }

        public void OnCaptured(CaptureResult captureResult)
        {
            try
            {
                // Check capture quality and throw an error if bad.
                if (!CheckCaptureResult(captureResult)) return;

                // Create bitmap
                foreach (Fid.Fiv fiv in captureResult.Data.Views)
                {
                    var finguer = CreateBitmap(fiv.RawImage, fiv.Width, fiv.Height);

                    if (finguer != null)
                    {

                        GuardarImagen(finguer);
                        // fingerPrint?.Invoke(finguer);
                        CancelCaptureAndCloseReader(OnCaptured);
                        isCapture?.Invoke(true);
                        validateMatch(finguer);
                    }
                }
            }
            catch (Exception ex)
            {
                // Send error message, then close form
                // SendMessage(Action.SendMessage, "Error:  " + ex.Message);
            }
        }

        public void GuardarImagen(Image img)
        {
            string ruta = Path.Combine("C:\\Img", "NombreArch2.jpg");
            
            Image Imagen = img;

            Imagen.Save(ruta, ImageFormat.Jpeg);
        }

        private byte[] ImageToByteArray(Bitmap imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        private async void validateMatch(Bitmap image)
        {
            var response = true;//await validateIdentity(ImageToByteArray(image));
            isMatch?.Invoke(response);
        }

        private async Task<bool> validateIdentity(byte[] image)
        {
            try
            {
                if (image != null)
                {
                    ModelFingerPrint model = new ModelFingerPrint();
                    model.TypeFinger = "";
                    model.Image = image;
                    model.Identification = identification;
                    var json = JsonConvert.SerializeObject(model);
                    var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
                    var url = string.Format("/Api/Authentication/validateFingerprint");
                    CancellationTokenSource cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    response = await client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    result = JsonConvert.DeserializeObject<ResponseMatch>(await response.Content.ReadAsStringAsync());
                    if (result.CodeError == 200)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (TaskCanceledException tkex)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }

    class ModelFingerPrint
    {
        public string TypeFinger { get; set; }

        public int Identification { get; set; }

        public byte[] Image { get; set; }
    }

    public class ResponseMatch
    {
        public int CodeError { get; set; }

        public string Message { get; set; }

        public object LisObject { get; set; }
    }
}
