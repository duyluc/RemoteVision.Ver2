using Basler.Pylon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcpSupport;
using PyVisionSupport;
using PylonController;
using System.Net;

namespace Client.Ver1
{
    public partial class FrmClient : Form
    {
        private string UnitId { get; set; }
        private string SerialNumber { get; set; }
        private Terminal OutputImage { get; set; }
        private Stopwatch StopWatch { get; set; }
        private PyCamera mCamera { get; set; }
        private ClientTcp Client { get; set; }
        private IPEndPoint ServerEp { get; set; }
        public Dictionary<string,Terminal> Output { get; set; }

        public FrmClient()
        {
            InitializeComponent();
            this.UnitId = "0x01";
            this.cbImageFormat.DefaultName = "Image Format";
            this.slExposure.DefaultName = "Exposure";
            this.slGain.DefaultName = "Gain";
            this.slWidth.DefaultName = "Width";
            this.slHeight.DefaultName = "Height";
            UpdateDevice();
            this.FormClosing += ClientMain_FormClosing;
            this.StopWatch = new Stopwatch();
            this.OutputImage = new Terminal("OutputImage");
            this.Output = new Dictionary<string, Terminal>();
            this.Output.Add(this.OutputImage.Name, this.OutputImage);
        }

        private void ClientMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.DestroyCamera();
        }

        private void UpdateDevice()
        {
            try
            {
                List<ICameraInfo> _listCameraInfo = PyCamera.FindCameras();
                ListView.ListViewItemCollection items = this.lvCameras.Items;
                foreach (ICameraInfo camerainfo in _listCameraInfo)
                {
                    bool newitem = true;
                    foreach (ListViewItem item in items)
                    {
                        ICameraInfo tag = item.Tag as ICameraInfo;
                        if (tag[CameraInfoKey.FullName] == camerainfo[CameraInfoKey.FullName])
                        {
                            tag = camerainfo;
                            newitem = false;
                            break;
                        }
                    }

                    if (newitem)
                    {
                        ListViewItem item = new ListViewItem(camerainfo[CameraInfoKey.FriendlyName]);
                        item.Tag = camerainfo;
                        this.lvCameras.Items.Add(item);
                    }
                }

                foreach (ListViewItem item in items)
                {
                    bool exists = false;
                    foreach (ICameraInfo camerainfor in _listCameraInfo)
                    {
                        if (((ICameraInfo)item.Tag)[CameraInfoKey.FullName] == camerainfor[CameraInfoKey.FullName])
                        {
                            exists = true;
                            break;
                        }
                        if (!exists)
                        {
                            this.lvCameras.Items.Remove(item);
                        }
                    }
                }
            }
            catch (Exception t)
            {

            }
        }

        public void EnableButton(bool enable = true)
        {

        }

        private void btnRefreshLV_Click(object sender, EventArgs e)
        {
            //this.UpdateDevice();
            string ip = this.tbIPAddress.Text.Split(':')[0];
            int port = int.Parse(this.tbIPAddress.Text.Split(':')[1]);
            this.Client = new ClientTcp(ip, port);
            this.ServerEp = this.Client.ServerEP;
            byte[] lenth = BitConverter.GetBytes(1);
            byte[] senddata = new byte[5];
            Array.Copy(lenth, senddata, 4);
            senddata[4] = 0x06;
            Task _ = this.Client.Command(senddata);
        }

        private void lvCameras_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = this.lvCameras.SelectedItems.Count;
            if (this.mCamera != null)
            {
                this.DestroyCamera();
            }

            if (index > 0)
            {
                ListViewItem item = this.lvCameras.SelectedItems[0];
                ICameraInfo _selectedCamera = item.Tag as ICameraInfo;
                try
                {
                    this.mCamera = new PyCamera(_selectedCamera);
                    this.mCamera.ConnectionLost += MCamera_ConnectionLost;
                    this.mCamera.CameraOpened += MCamera_CameraOpened;
                    this.mCamera.CameraClosed += MCamera_CameraClosed;
                    this.mCamera.StreamGrabber.GrabStarted += StreamGrabber_GrabStarted;
                    this.mCamera.StreamGrabber.ImageGrabbed += StreamGrabber_ImageGrabbed;
                    this.mCamera.StreamGrabber.GrabStopped += StreamGrabber_GrabStopped;

                    this.mCamera.Open();

                    this.cbImageFormat.Parameter = this.mCamera.Parameters[PLCamera.PixelFormat];
                    if (mCamera.Parameters.Contains(PLCamera.ExposureTimeAbs))
                    {
                        this.slExposure.Parameter = this.mCamera.Parameters[PLCamera.ExposureTimeAbs];
                    }
                    else
                    {
                        this.slExposure.Parameter = this.mCamera.Parameters[PLCamera.ExposureTime];
                    }
                    this.slWidth.Parameter = this.mCamera.Parameters[PLCamera.Width];
                    this.slHeight.Parameter = this.mCamera.Parameters[PLCamera.Height];
                    if (this.mCamera.Parameters.Contains(PLCamera.GainAbs))
                    {
                        this.slGain.Parameter = this.mCamera.Parameters[PLCamera.GainAbs];
                    }
                    else
                    {
                        this.slGain.Parameter = mCamera.Parameters[PLCamera.Gain];
                    }

                    this.SerialNumber = _selectedCamera[CameraInfoKey.SerialNumber];

                }
                catch (Exception t)
                {
                    this.ShowException(t);
                }
            }
        }

        private void StreamGrabber_GrabStopped(object sender, GrabStopEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<GrabStopEventArgs>(StreamGrabber_GrabStopped), sender, e);
                return;
            }
            this.EnableButton(true);
        }

        private void StreamGrabber_ImageGrabbed(object sender, ImageGrabbedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(StreamGrabber_ImageGrabbed), sender, e.Clone());
                return;
            }
            try
            {
                IGrabResult grabResult = e.GrabResult;
                if (grabResult.GrabSucceeded)
                {
                    LImage outputimage = new LImage(grabResult, this.UnitId, this.SerialNumber);
                    this.OutputImage.SetValue(outputimage);
                    this.Display.Image = outputimage.BitmapImage;
                }
                string ip = this.tbIPAddress.Text.Split(':')[0];
                int port = int.Parse(this.tbIPAddress.Text.Split(':')[1]);
                this.Client = new ClientTcp(ip, port);
                this.ServerEp = this.Client.ServerEP;
                Task _ = this.Client.Command(TcpSupport.Serialize.ObjectToByteArray(this.Output));
            }
            catch (Exception t)
            {
                this.ShowException(t);
            }
        }

        private void StreamGrabber_GrabStarted(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<EventArgs>(StreamGrabber_GrabStarted), sender, e);
                return;
            }
            this.EnableButton(false);
        }

        private void MCamera_CameraClosed(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<EventArgs>(MCamera_CameraClosed), sender, e);
                return;
            }
            this.EnableButton(false);
        }

        private void MCamera_CameraOpened(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<EventArgs>(MCamera_CameraOpened), sender, e);
                return;
            }
            EnableButton(true);
        }

        private void MCamera_ConnectionLost(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(MCamera_ConnectionLost), sender, e);
                return;
            }

            // Close the camera object.
            DestroyCamera();
            // Because one device is gone, the list needs to be updated.
            this.UpdateDevice();
        }

        private void DestroyCamera()
        {
            try
            {
                if (mCamera != null)
                {
                    cbImageFormat.Parameter = null;
                    slExposure.Parameter = null;
                    slGain.Parameter = null;
                    slWidth.Parameter = null;
                    slHeight.Parameter = null;
                }
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }

            // Destroy the camera object.
            try
            {
                if (mCamera != null)
                {
                    mCamera.Close();
                    mCamera.Dispose();
                    mCamera = null;
                }
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }
        }

        private void ShowException(Exception exception)
        {
            MessageBox.Show("Exception caught:\n" + exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            try
            {
                this.StartStopWatch();
                Configuration.AcquireSingleFrame(mCamera, null);
                mCamera.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception t)
            {
                this.ShowException(t);
            }
        }

        public void StartStopWatch()
        {
            this.StopWatch.Reset();
            this.StopWatch.Start();
        }

        public void ShowStopWatchTime()
        {
            this.lbTactTime.Invoke(new Action(() => { this.lbTactTimer.Text = $"Timer: {this.StopWatch.ElapsedMilliseconds} ms"; }));
        }
    }
}
