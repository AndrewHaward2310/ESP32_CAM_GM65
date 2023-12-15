using System.Windows.Forms;

namespace ESP_Camera
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            EspClass esp = new EspClass();
            esp.Show();
        }

        private void saveImageCapture(Bitmap capturedImage, string barcodeData)
        {
            Bitmap image = new Bitmap(capturedImage);
            string savePath = Path.Combine("D:\\Data_Image", barcodeData + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg");
            image.Save(savePath);
        }

        private async void btntest_Click(object sender, EventArgs e)
        {
            EspClass esp = new EspClass();
            esp.Show();
            MessageBox.Show("Test Mode");
            await Task.Delay(3000);
            while (true)
            {
                esp.txtBarcode.Text = await esp.GetBarcode();
                await Task.Delay(1000);
                await esp.ToggleLight();
                await Task.Delay(3000);
                Bitmap capturedImage = await esp.GetImage();
                if (capturedImage != null)
                {
                    saveImageCapture(capturedImage, "");
                    esp.pictureBox1.Image?.Dispose();
                    esp.pictureBox1.Image = capturedImage;
                    esp.pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
                }
                await esp.ToggleLight();
                await Task.Delay(3000);
            }
        }
    }
}