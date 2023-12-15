using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;
using System.IO.Ports;
using static System.Net.Mime.MediaTypeNames;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Drawing.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace ESP_Camera
{
    public partial class EspClass : Form
    {
        string IP = "192.168.127.1";

        int Alarm = 0;
        private WebSocket ws;
        private SerialPort serialPort;
        private HttpClient httpClient = new HttpClient();
        public event EventHandler<bool> LightStateChanged;
        public event EventHandler<bool> RS232State;
        private bool isConnected = false;
        public bool IsLightOn { get; private set; } = false;
        public bool IsRs232On { get; private set; } = true;

        private string receivedData = "";
        private string textValue = "";
        public EspClass()
        {
            InitializeComponent();
            InitializeFramesizeComboBox();
            InitializeCameraProperties();
            InitializeSpecialEffect();
            InitializeWhiteBalance();
            Task.Run(async () =>
            {
                await ConnectToWebSocket();
            });
            txtBarcode.KeyPress += TxtBarcode_KeyPress;
        }

        private void InitializeSerialPort()
        {
            serialPort = new SerialPort();
            serialPort.BaudRate = 9600;
            serialPort.DataReceived += SerialPort_DataReceived;
        }

        private async void TxtBarcode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                textValue = txtBarcode.Text;

                await ToggleLight();
                await Task.Delay(3000);
                Bitmap capturedImage = await GetImage();
                if (capturedImage != null)
                {
                    // Xóa ảnh cũ trước khi hiển thị ảnh mới
                    if (pictureBox1.Image != null)
                    {
                        pictureBox1.Image.Dispose();
                        pictureBox1.Image = null;
                    }

                    saveImageCapture(capturedImage, textValue);
                    pictureBox1.Image = capturedImage;
                    pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
                }
                await Task.Delay(1000);
                await ToggleLight();

                e.Handled = true;
            }
        }



        private async Task ConnectToWebSocket()
        {
            string WebSocketUrl = "ws://" + IP + ":8181/ws";
            ws = new WebSocket(WebSocketUrl);
            HandleWebSocketEvents();

            while (true)
            {
                if (!isConnected)
                {
                    try
                    {
                        await Task.Run(() => ws.Connect());
                        isConnected = true;
                    }
                    catch (Exception ex)
                    {
                        // Xử lý lỗi kết nối ở đây
                        MessageBox.Show("Lỗi kết nối WebSocket: " + ex.Message);
                        await Task.Delay(5000);
                    }
                }
                await Task.Delay(1000);
            }
            
        }


        private Dictionary<string, int> FrameSizeMapping = new Dictionary<string, int>
        {
            { "96x96", 0 },
            { "160x120", 1 },
            { "176x144", 2 },
            { "240x176", 3 },
            { "240x240", 4 },
            { "320x240", 5 },
            { "400x296", 6 },
            { "480x320", 7 },
            { "640x480", 8 },
            { "800x600", 9 },
            { "1024x768", 10 },
            { "1280x720", 11 },
            { "1280x1024", 12 },
            { "1600x1200", 13 }
        };

        private Dictionary<string, int> SpeacialEffectMapping = new Dictionary<string, int>
        {
            { "0: No effect", 0 },
            { "1: Negative", 1 },
            { "2: Grayscale", 2 },
            { "3: Red tint", 3 },
            { "4: Green tint", 4 },
            { "5: Blue tint", 5 },
            { "6: Sepia", 6 }
        };

        private Dictionary<string, int> WhiteBalanceMapping = new Dictionary<string, int>
        {
            { "0: Auto", 0 },
            { "1: Sunny", 1 },
            { "2: Cloudy", 2 },
            { "3: Office", 3 },
            { "4: Home", 4 }
        };

        private void InitializeFramesizeComboBox()
        {
            string[] FrameSizes = {
                "1600x1200", "1280x1024", "1280x720", "1024x768",
                "800x600", "640x480", "480x320", "400x296",
                "320x240", "240x240", "240x176", "176x144",
                "160x120", "96x96"
            };

            FrameSizeBox.Items.AddRange(FrameSizes);
            FrameSizeBox.SelectedIndex = 5;
        }

        private void InitializeSpecialEffect()
        {
            string[] SpecialEffect =
            {
                "0: No effect", "1: Negative", "2: Grayscale", "3: Red tint", "4: Green tint", "5: Blue tint", "6: Sepia"
            };
            SpecialEffectBox.Items.AddRange(SpecialEffect);
            SpecialEffectBox.SelectedIndex = 0;
        }

        private void InitializeWhiteBalance()
        {
            string[] WhiteBalance =
            {
                "0: Auto", "1: Sunny", "2: Cloudy", "3: Office", "4: Home"
            };
            WhiteBalanceBox.Items.AddRange(WhiteBalance);
            WhiteBalanceBox.SelectedIndex = 0;
        }


        public void InitializeCameraProperties()
        {
            // Đọc giá trị hiện tại của các thuộc tính và sử dụng chúng làm giá trị mặc định
            /*quanlity = GetCameraProperty("quanlity");
            brightness = GetCameraProperty("brightness");
            contrast = GetCameraProperty("contrast");
            saturation = GetCameraProperty("saturation");
            setspecialeffect = GetCameraProperty("special_effect");
            setwhitebalance = GetCameraProperty("awb");
            setawb_gain = GetCameraProperty("awb_gain");
            set_wb_mode = GetCameraProperty("wb_mode");
            setaec_value = GetCameraProperty("aec_value");
            set_expousre_ctrl = GetCameraProperty("aec");
            setaec = GetCameraProperty("aec2");
            set_ae_level = GetCameraProperty("ae_level");
            setgain_ctrl = GetCameraProperty("agc");
            set_agc_gain = GetCameraProperty("agc_gain");
            set_gainceiling = GetCameraProperty("gainceiling");
            set_bpc = GetCameraProperty("bpc");
            set_wpc = GetCameraProperty("wpc");
            set_raw_gma = GetCameraProperty("raw_gma");
            set_lenc = GetCameraProperty("lenc");
            set_hmirror = GetCameraProperty("hmirror");
            set_vflip = GetCameraProperty("vflip");
            set_dcw = GetCameraProperty("dcw");
            set_colorbar = GetCameraProperty("colorbar");*/
        }

        private void HandleWebSocketEvents()
        {
            ws.OnMessage += (sender, e) =>
            {
                // xử lý message nhận từ server
                string message = e.Data;
                if (message == "1")
                {
                    Invoke((MethodInvoker)(() => btnToggleLed.Text = "Led On"));
                }

                else if (message == "2")
                {
                    Invoke((MethodInvoker)(() => btnToggleLed.Text = "Led Off"));
                }

                else if (message == "3")
                {
                    MessageBox.Show("ERROR: Failed to switch RS232 to USB");
                }

                else if (message == "4")
                {
                    Invoke((MethodInvoker)(() => btnRs232.Text = "USB"));
                }

                else if (message == "5")
                {
                    MessageBox.Show("ERROR: Failed to switch USB to RS232");
                }

                else if (message == "6")
                {
                    Invoke((MethodInvoker)(() => btnRs232.Text = "RS232"));
                }
                else if (message == "7")
                {
                    MessageBox.Show("Barcode reset Failed!");
                }
                else if (message == "8")
                {
                    MessageBox.Show("Barcode reset Successfully!");
                }
                else if (message == "12")
                {
                    MessageBox.Show("Reset Camera Successfully!");
                }
                else if (message == "16")
                {
                    MessageBox.Show("Camera init failed");
                }
            };

            ws.OnOpen += (sender, e) =>
            {
                // Xử lý khi kết nối thành công
                isConnected = true;
            };

            ws.OnError += (sender, e) =>
            {
                // Xử lý khi có lỗi xảy ra
                Console.WriteLine("Lỗi WebSocket: " + e.Message);
                isConnected = false;
            };

            ws.OnClose += (sender, e) =>
            {
                // Xử lý khi đóng kết nối
                isConnected = false;
            };
        }

        private async void EspClass_Load(object sender, EventArgs e)
        {

        }

        bool SetIP(string ip)
        {
            IP = ip;
            return true;
        }

        bool SetAlarm(int alarm)
        {
            Alarm = alarm;
            return true;
        }

        bool ResetAlarm()
        {
            Alarm = 0;
            return true;
        }

        /*public static DialogResult MyMessageBox(int erridx, MessageBoxButtons btn = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information, string extramsg = "", string title = "System message")
        {
            return (MessageBox.Show(mymsg[erridx, GlobalVar.SetLanguage] + "\n\n" + extramsg, title, btn, icon));
        }

        public static DialogResult MyMessageBox(int erridx, string extramsg, MessageBoxButtons btn = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Hand, string title = "System message")
        {
            return (MessageBox.Show(mymsg[erridx, GlobalVar.SetLanguage] + "\n\n" + extramsg, title, btn, icon));
        }*/
        private static string[,] mymsg = {    {  "Can not connect database server","Không thể kết nối được cơ sở dữ liệu"}, //0
                                              { "Can not connect to camera. Please check camera cable","Không thể kết nối với camera\nKiểm tra lại cáp camera"},
                                              { "You have entered invalid OperatorID or PASSWORD!!! \n Try it again","Mật mã hoặc kỹ thuật viên ID nhập vào bị sai. Hãy thử lại !"},
                                              { "Do you really want to exit application?","Bạn có muốn thoát khỏi chương trình ?"}, //3
                                              { "Do you want to Logout ?","Bạn có muốn thoát khỏi phiên làm việc này không ?"},
                                              { "Your computer's configuration is not suitable to STARGEL Software\nPlease contact to us for more information","Máy tính của bạn không thích hợp để chạy phần mềm STARGEL\nHãy liên hệ với chúng tôi để biết thêm chi tiết"},
                                              { "ALARM: This software is demo only\nPlease contact to STARGEL10 Reader distributor to get the license","Phần mềm này của bạn là bản demo, chưa có bản quyền\nHãy liên hệ với đại lý của chúng tôi để lấy mã bản quyền"},
                                              { "Can not biding data from Database","Không thể thiết lập được liên kết từ cơ sở dữ liệu"}, //7
                                              { "Do you really want to delete current data in database ?","Bạn có muốn xoá dữ liệu hiện thời không ?"},
                                              { "Your data has been deleted !","Dữ liệu đã xoá thành công !"}, //9
                                              { "Can not delete selected data. Please contact to us for more information","Không thể xoá được dữ liệu đang chọn\nHãy liên hệ với chúng tôi để biết thêm chi tiết"},
                                              { "Selected picture is saved as filename:","Dữ liệu hình ảnh đã được lưu trữ bằng tệp:"}, //11
                                              { "There are a lot of data in your table\nDo your REALLY WANT TO EXPORT all current data to your Local Network ?","Có rất nhiều dữ liệu trong bảng kết quả\n Bạn có muốn gửi hết ra mạng cục bộ không ?"},
                                              { "Can not connect to Local server","Không thể kết nối máy chủ trong mạng"}, //13
                                              { "Export Data to local network is ok","Đã kết xuất xong kết quả ra mạng cục bộ"},
                                              { "Report file is created:","Báo cáo đã được tạo ra bằng tệp:"}, //15
                                              { "You have no right to use DeleteAll function !","Bạn không có quyền dùng tính năng xoá tất cả !"},
                                              { "Do you really want to delete ALL CURRENT DATA in database ?","Bạn có thực sự muốn xoá hết dữ liệu hiện thời trong bảng không ?"},
                                              { "Current data is updated !","Dữ liệu bệnh nhân hiện thời đã được cập nhật !"}, //18
                                              { "Current data can not updated because:","Dữ liệu hiện thời không thể cập nhật được bởi vì:"},
                                              { "Configuration's Data has been saved and actived !","Tham số đã được lưu lại và được sử dụng ngay !"}, //20
                                              { "Calibrated data has been saved !","Tham số hiệu chỉnh đã được lưu trữ thành công !"},
                                              { "Please insert the CALIBRATED GELCARD into Reader then press OK","Hãy cho card chuyên dùng để hiệu chỉnh vào máy đọc rồi nhấn phím OK ở đây để tiếp tục"},
                                              { "Can not run AutoCalibrate function!\nPlease select Calibrated Gelcard correctly","Không thế hiệu chỉnh tự động được\nXem lại card chuyên dùng hoặc liên lạc với chúng tôi ngay !"},
                                              { "Auto Calibrate is done. Press button [Save Data] to save data to database","Tự động hiệu chỉnh đã xong. Hãy nhấn [Save Data] để lưu trữ tham số này lại"},
                                              { "Can not save data into Database. Check database server !","Không lưu trữ dữ liệu vào CSDL. Hãy kiểm tra dịch vụ CSDL !"},
                                              { "This patient is exiting in Database with different result:","Đã có bệnh nhân trùng thông tin có kết quả xét nghiệm khác:"}, //26
                                              { "You must fill in enough information !","Bạn phải nhập đầy đủ dữ liệu !"}, // 27
                                              { "Patient's information are not correct (same information). Please re-input !","Thông tin 2 bện nhân trùng nhau. Bạn phải nhập lại thông tin !"},
                                              { "Are you sure that the result are correct ?\nPress YES to accept this result ! ","Bạn hãy KIỂM TRA thật cẩn thận kết quả đang hiển thị\n\n Nếu bạn chắc chắn là đúng, hãy nhấn YES để lưu kết quả này!"},
                                              { "This gelcard was ready used. Please check and use another gelcard !","Gelcard này đã sử dụng rồi. Hãy kiểm tra lại và sử dụng Gelcard khác !"},
                                              { "The report for error is save as below name\nPlease send it to our service center. THANKS !","Tệp báo cáo lỗi đã được lưu theo tên ở dưới đây\nHãy gửi nó đến trung tâm dịch vụ của chúng tôi ngay. XIN CẢM ƠN !"},
                                              { "Please insert new Gelcard then Press BUTTON on reader ","Hãy cho gelcard thứ 2 vào rồi nhấn phím BUTTON trên máy đọc"},
                                              { "Please use Neutral gelcard !", "Gelcard thứ 2 phải là Neutral card !"},
                                              { "Please use AHG gelcard !", "Gelcard thứ 2 phải là AHG card !"}, //34
                                              { "Would you like to use another card for \n Antibody Identification - Saline Test ?","Bạn có muốn đọc thêm kết quả từ card khác cho\n Xét nghiệm: Sàng lọc kháng thể bất thường môi trường muối ???"},
                                              { "Please insert the Gelcard into Reader machine \n and then PRESS OK to continue !","Bạn hãy cho Gelcard vào máy đọc rồi nhấn phím OK ở đây để tiếp tục !"}, // 36
                                              { "All pictures of gelcard are created in folder REPORT_PICTURE\nName of file: barcode_partient.JPG","Hình ảnh các Gelcard được ghi trong thư mục REPORT_PICTURE\nTên file:  mã barcode_thông tin bệnh nhân.JPG"}, // 36
                                              { "Can not find this partient's barcode. Please check the list of partient","Không tìm thấy mã barcode bệnh nhân này\nHãy xem lại danh sách bệnh nhân"}, // 38
                                              { "Not allow insert this blood bag without crossmatch testing result","Không cho phép nhập túi máu này mà chưa qua xét nghiệm chéo tương hợp"},
                                              { "Waring: Blood group is different to partient's group blood !", "Cảnh báo: Nhóm máu không trùng với nhóm máu của bệnh nhân !"}, // 40
                                              { "Error: Not enough information to add new blood bag","Không đủ dữ liệu để thêm túi máu mới vào ngân hàng máu"},
                                              { "This card is exprired time. Can not use it !","Card này quá hạn sử dụng. Không dùng được !"}, // 42
                                              { "Can not write file. File name is invalid ! ", "Không ghi được tệp. Tên tệp không hợp lệ ! "}, //43
                                              { "LIS Data is invalid. Please check your LIS syntax", "Dữ liệu từ LIS không hợp lệ. Hãy kiểm tra lại cú pháp từ LIS !"}, //44
                                              { "Can not load LOGO. Please register LOGO and set it as Default","Không nạp được Logo. Hãy đăng ký 1 Logo mới và thiết lập mặc định cho nó"}, //45
                                              { "You have no right to delete the results !","Bạn không có quyền xóa dữ liệu kết quả !"}, //46
                                              { "Please select a tube as Auto Control","Hãy chọn 1 giếng xét nghiệm Auto Control"},

                                              { "","" }
                                           };

        private static string[,] mymsg1000 = {    {  "Normal closure, meaning that the purpose for which the connection was established has been fulfilled","Đóng kết nối bình thường, có nghĩa là mục đích mà kết nối được thiết lập đã được thực hiện"}, //1000
                                              { "An endpoint is \"going away\", such as a server going down or a browser having navigated away from a page.","Một điểm cuối đang \"đóng\", ví dụ máy chủ tắt hoặc trình duyệt đã điều hướng ra khỏi một trang."}, //1001
                                              { "An endpoint is terminating the connection due to a protocol error.","Một điểm cuối đang kết thúc kết nối do lỗi giao thức."}, // 1002
                                              { "An endpoint is terminating the connection because it has received a type of data it cannot accept (e.g., an endpoint that understands only text data MAY send this if it receives a binary message).","Một điểm cuối đang kết thúc kết nối vì nó đã nhận được một loại dữ liệu mà nó không thể chấp nhận (ví dụ, một điểm cuối chỉ hiểu dữ liệu văn bản có thể gửi điều này nếu nó nhận được một tin nhắn nhị phân)."}, //1003
                                              { "Reserved. The specific meaning might be defined in the future", "Được dữ trữ. Ý nghĩa cụ thể có thể được định nghĩa trong tương lai." }, // 1004
                                              { "No status code was actually present", "Không có mã trạng thái thực sự" }, //1005
                                              { "The connection was closed abnormally, e.g., without sending or receiving a Close control frame", "Kết nối đã bị đóng một cách không bình thường, ví dụ, không gửi hoặc nhận một khung kiểm soát Close" }, //1006
                                              { "An endpoint is terminating the connection because it has received data within a message that was not consistent with the type of the message (e.g., non-UTF-8 data within a text message)", "Một điểm cuối đang kết thúc kết nối vì nó đã nhận dữ liệu trong một tin nhắn không phù hợp với loại của tin nhắn (ví dụ, dữ liệu không phải UTF-8 trong một tin nhắn văn bản)" }, //1007
                                              { "An endpoint is terminating the connection because it has received a message that \"violates its policy\". This reason is given either if there is no other suitable reason, or if there is a need to hide specific details about the policy", "Một điểm cuối đang kết thúc kết nối vì nó đã nhận được một tin nhắn vi phạm \\\"chính sách\\\" của nó. Lý do này được đưa ra nếu không có lý do phù hợp khác hoặc nếu cần ẩn đi các chi tiết cụ thể về chính sách" }, //1008
                                              { "An endpoint is terminating the connection because it has received a message that is too big for it to process", "Một điểm cuối đang kết thúc kết nối vì nó đã nhận một tin nhắn quá lớn mà nó không thể xử lý" }, //1009
                                              { "An endpoint (client) is terminating the connection because it has expected the server to negotiate one or more extensions, but the server didn't return them in the response message of the WebSocket handshake","Một điểm cuối (khách hàng) đang kết thúc kết nối vì nó đã mong đợi máy chủ thỏa thuận một hoặc nhiều tiện ích mở rộng, nhưng máy chủ không trả chúng trong tin nhắn phản hồi của cuộc bắt tay WebSocket" }, //1010
                                              { "A server is terminating the connection because it encountered an unexpected condition that prevented it from fulfilling the request", "Một máy chủ đang kết thúc kết nối vì nó đã gặp tình huống không mong đợi mà đã ngăn nó thực hiện yêu cầu" }, // 1011
                                              { "The connection was closed due to a failure to perform a TLS handshake (e.g., the server certificate can't be verified)", "Kết nối đã bị đóng vì không thể thực hiện cuộc bắt tay TLS (ví dụ, chứng chỉ máy chủ không thể xác minh" } //1015
        };
        public string GetAlarm(short code)
        {
            return "";
        }
        public async Task<Bitmap> GetImage() // Hàm chụp ảnh
        {
            string esp32CamURL = $"http://{IP}/capture";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(esp32CamURL);

                    // Kiểm tra xem yêu cầu có thành công không
                    if (response.IsSuccessStatusCode)
                    {
                        // Đọc dữ liệu hình ảnh từ nội dung của phản hồi
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                        // Chuyển đổi dữ liệu hình ảnh thành Bitmap
                        using (MemoryStream stream = new MemoryStream(imageBytes))
                        {
                            Bitmap image = new Bitmap(stream);
                            return image;
                        }
                    }
                    else
                    {
                        // Xử lý lỗi khi yêu cầu không thành công
                    }
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi khi có ngoại lệ xảy ra
                }
            }
            return null;
        }


        public async Task<string> GetBarcode() // Hàm đọc mã vạch
        {
            bool ContainsLetter(string input)
            {
                return input.Any(char.IsLetter);
            }

            string esp32CamURL = $"http://{IP}/gm65_scan";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = client.GetAsync(esp32CamURL).Result;

                    // Kiểm tra xem yêu cầu có thành công không
                    if (response.IsSuccessStatusCode)
                    {
                        // Đọc dữ liệu barcode từ nội dung của phản hồi
                        byte[] barcodeBytes = response.Content.ReadAsByteArrayAsync().Result;
                        string barcodeData = Encoding.UTF8.GetString(barcodeBytes);
                        if (!string.IsNullOrEmpty(barcodeData) && (barcodeData.Length >= 20 && !ContainsLetter(barcodeData)))
                        {
                            return barcodeData;
                        }

                    }
                    else
                    {
                        // Xử lý lỗi khi yêu cầu không thành công

                    }
                }
                catch (Exception ex)
                {
                    // Xử lý lỗi khi có ngoại lệ xảy ra
                }
            }
            return null;
        }

        public async Task<bool> ToggleLight() // Bật tắt led
        {
            if (ws.IsAlive)
            {
                if (IsLightOn == false)
                {
                    ws.Send("1");
                    IsLightOn = true;
                    LightStateChanged?.Invoke(this, IsLightOn);
                }
                else
                {
                    ws.Send("2");
                    IsLightOn = false;
                    LightStateChanged?.Invoke(this, IsLightOn);
                }
            }
            return IsLightOn;
        }

        public async Task<bool> Rs232Connect()
        {
            if (ws.IsAlive)
            {
                if (IsRs232On = true) // chuyển sang quét barcode bằng usb
                {
                    ws.Send("3");
                    IsRs232On = false;
                    RS232State?.Invoke(this, IsRs232On);
                }
                else // chuyển sang quét barcode bằng rs232
                {
                    ws.Send("4");
                    IsRs232On = true;
                    RS232State?.Invoke(this, IsRs232On);
                }
            }
            return IsRs232On;
        }

        public async Task SetFrameSize(string frameSize)
        {
            if (FrameSizeMapping.TryGetValue(frameSize, out int frameSizeValue))
            {
                string esp32CamURL = $"http://{IP}/control?var=framesize&val={frameSizeValue}";
                HttpResponseMessage response = await httpClient.GetAsync(esp32CamURL);
            }
        }

        public int GetCameraProperty(string propertyName) // Get thuộc tính hiện tại của camera
        {
            string esp32CamURL = $"http://{IP}/control?var={propertyName}";
            HttpResponseMessage response = httpClient.GetAsync(esp32CamURL).Result;
            if (response.IsSuccessStatusCode)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                if (int.TryParse(content, out int value))
                {
                    return value;
                }
            }
            return -1; // Trả về giá trị mặc định hoặc giá trị không hợp lệ
        }

        public void SetCameraProperty(string propertyName, int value) // Set thuộc tính cho camera
        {
            string esp32CamURL = $"http://{IP}/control?var={propertyName}&val={value}";
            HttpResponseMessage response = httpClient.GetAsync(esp32CamURL).Result;
            if (!response.IsSuccessStatusCode)
            {
                // Xử lý lỗi khi yêu cầu không thành công
                throw new Exception($"Failed to set {propertyName}.");
            }
        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void label25_Click(object sender, EventArgs e)
        {

        }

        private void label55_Click(object sender, EventArgs e)
        {

        }

        private void label69_Click(object sender, EventArgs e)
        {

        }

        private void button14_Click(object sender, EventArgs e)
        {
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void saveImageCapture(Bitmap capturedImage, string barcodeData)
        {
            Bitmap image = new Bitmap(capturedImage);
            string savePath = Path.Combine("D:\\Data_Image", barcodeData + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg");
            image.Save(savePath);
        }


        private void FrameSizeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedFramesize = FrameSizeBox.SelectedItem.ToString();
            SetFrameSize(selectedFramesize);
        }

        private void QuanlityBar_Scroll(object sender, EventArgs e)
        {
            quanlity = QuanlityBar.Value;
            SetCameraProperty("quanlity", quanlity);
        }

        private void BrightnessBar_Scroll(object sender, EventArgs e)
        {
            brightness = BrightnessBar.Value;
            SetCameraProperty("brightness", brightness);
        }

        private void ContrastBar_Scroll(object sender, EventArgs e)
        {
            contrast = ContrastBar.Value;
            SetCameraProperty("contrast", contrast);
        }

        private void SaturationBar_Scroll(object sender, EventArgs e)
        {
            saturation = SaturationBar.Value;
            SetCameraProperty("saturation", saturation);
        }

        private void SpecialEffectBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedSpecialEffect = SpecialEffectBox.SelectedItem.ToString();
            if (SpeacialEffectMapping.TryGetValue(selectedSpecialEffect, out int specialEffectValue))
            {
                SetCameraProperty("special_effect", specialEffectValue);
            }
        }

        private void btnAWB_Click(object sender, EventArgs e)
        {
            string txtAWB = btnAWB.Text;
            if (txtAWB == "ON")
            {
                setwhitebalance = 1;
                SetCameraProperty("awb", setwhitebalance);
                btnAWB.Text = "OFF";
            }
            else
            {
                setwhitebalance = 0;
                SetCameraProperty("awb", setwhitebalance);
                btnAWB.Text = "ON";
            }
        }

        private void btnAWBGain_Click(object sender, EventArgs e)
        {
            string txtAWBGain = btnAWBGain.Text;
            if (txtAWBGain == "ON")
            {
                setawb_gain = 1;
                SetCameraProperty("awb_gain", setawb_gain);
                btnAWBGain.Text = "OFF";
            }
            else
            {
                setawb_gain = 0;
                SetCameraProperty("awb_gain", setawb_gain);
                btnAWBGain.Text = "ON";
            }
        }

        private void WhiteBalanceBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedWhiteBalance = WhiteBalanceBox.SelectedItem.ToString();
            if (WhiteBalanceMapping.TryGetValue(selectedWhiteBalance, out int whiteBalanceValue))
            {
                SetCameraProperty("wb_mode", whiteBalanceValue);
            }
        }

        private void btnAEC_Click(object sender, EventArgs e)
        {
            string txtAEC = btnAEC.Text;
            if (txtAEC == "ON")
            {
                label55.Visible = false;
                label56.Visible = false;
                label57.Visible = false;
                ExposureBar.Visible = false;

                set_expousre_ctrl = 1;
                SetCameraProperty("aec", set_expousre_ctrl);
                btnAEC.Text = "OFF";
            }
            else
            {
                label55.Visible = true;
                label56.Visible = true;
                label57.Visible = true;
                ExposureBar.Visible = true;

                set_expousre_ctrl = 0;
                SetCameraProperty("aec", set_expousre_ctrl);
                btnAEC.Text = "ON";
            }
        }

        private void btnAEC2_Click(object sender, EventArgs e)
        {
            string txtAEC2 = btnAEC2.Text;
            if (txtAEC2 == "ON")
            {
                setaec = 1;
                SetCameraProperty("aec2", setaec);
                btnAEC2.Text = "OFF";
            }
            else
            {
                setaec = 0;
                SetCameraProperty("aec2", setaec);
                btnAEC2.Text = "ON";
            }
        }

        private void ExposureBar_Scroll(object sender, EventArgs e)
        {
            setaec_value = ExposureBar.Value;
            SetCameraProperty("aec_value", setaec_value);
        }

        private void AELevelBar_Scroll(object sender, EventArgs e)
        {
            set_ae_level = AELevelBar.Value;
            SetCameraProperty("ae_level", set_ae_level);
        }

        private void btnAGC_Click(object sender, EventArgs e)
        {
            string txtAGC = btnAGC.Text;
            if (txtAGC == "ON")
            {
                setgain_ctrl = 1;
                SetCameraProperty("agc", setgain_ctrl);
                btnAGC.Text = "OFF";
            }
            else
            {
                setgain_ctrl = 0;
                SetCameraProperty("agc", setgain_ctrl);
                btnAGC.Text = "ON";
            }
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            set_gainceiling = trackBar3.Value;
            SetCameraProperty("gainceiling", set_gainceiling);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            string txtBPC = btnBPC.Text;
            if (txtBPC == "ON")
            {
                set_bpc = 1;
                SetCameraProperty("bpc", set_bpc);
                btnBPC.Text = "OFF";
            }
            else
            {
                set_bpc = 0;
                SetCameraProperty("bpc", set_bpc);
                btnBPC.Text = "ON";
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string txtWPC = btnWPC.Text;
            if (txtWPC == "ON")
            {
                set_wpc = 1;
                SetCameraProperty("wpc", set_wpc);
                btnWPC.Text = "OFF";
            }
            else
            {
                set_wpc = 0;
                SetCameraProperty("wpc", set_wpc);
                btnWPC.Text = "ON";
            }
        }

        private void btnRawGMA_Click(object sender, EventArgs e)
        {
            string txtRawGMA = btnRawGMA.Text;
            if (txtRawGMA == "ON")
            {
                set_raw_gma = 1;
                SetCameraProperty("raw_gma", set_raw_gma);
                btnRawGMA.Text = "OFF";
            }
            else
            {
                set_raw_gma = 0;
                SetCameraProperty("raw_gma", set_raw_gma);
                btnRawGMA.Text = "ON";
            }
        }

        private void btnLens_Click(object sender, EventArgs e)
        {
            string txtLens = btnLens.Text;
            if (txtLens == "ON")
            {
                set_lenc = 1;
                SetCameraProperty("lenc", set_lenc);
                btnLens.Text = "OFF";
            }
            else
            {
                set_lenc = 0;
                SetCameraProperty("lenc", set_lenc);
                btnLens.Text = "ON";
            }
        }

        private void btnHMirror_Click(object sender, EventArgs e)
        {
            string txtHMirror = btnHMirror.Text;
            if (txtHMirror == "ON")
            {
                set_hmirror = 1;
                SetCameraProperty("hmirror", set_hmirror);
                btnHMirror.Text = "OFF";
            }
            else
            {
                set_hmirror = 0;
                SetCameraProperty("hmirror", set_hmirror);
                btnHMirror.Text = "ON";
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            string txtVFlip = label.Text;
            if (txtVFlip == "ON")
            {
                set_vflip = 1;
                SetCameraProperty("vflip", set_vflip);
                label.Text = "OFF";
            }
            else
            {
                set_vflip = 0;
                SetCameraProperty("vflip", set_vflip);
                label.Text = "ON";
            }
        }

        private void btnDCW_Click(object sender, EventArgs e)
        {
            string txtDCW = btnDCW.Text;
            if (txtDCW == "ON")
            {
                set_dcw = 1;
                SetCameraProperty("dcw", set_dcw);
                btnDCW.Text = "OFF";
            }
            else
            {
                set_dcw = 0;
                SetCameraProperty("dcw", set_dcw);
                btnDCW.Text = "ON";
            }
        }

        private void btnColorBar_Click(object sender, EventArgs e)
        {
            string txtColorBar = btnColorBar.Text;
            if (txtColorBar == "ON")
            {
                set_colorbar = 1;
                SetCameraProperty("colorbar", set_colorbar);
                btnColorBar.Text = "OFF";
            }
            else
            {
                set_colorbar = 0;
                SetCameraProperty("colorbar", set_colorbar);
                btnColorBar.Text = "ON";
            }
        }

        private async void btnToggleLed_Click(object sender, EventArgs e)
        {
            await ToggleLight();
        }

        private async void btnRs232_Click(object sender, EventArgs e)
        {
            await Rs232Connect();
        }

        private async void btnCaptureImage_Click(object sender, EventArgs e)
        {
            await ToggleLight();
            await Task.Delay(3000);
            Bitmap capturedImage = await GetImage();
            if (capturedImage != null)
            {
                saveImageCapture(capturedImage, textValue);
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = capturedImage;
                pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
            }
            await ToggleLight();
        }

        private async void btnReadBarcode_Click(object sender, EventArgs e)
        {
            string barcodeData = await GetBarcode();
            if (!string.IsNullOrEmpty(barcodeData))
            {
                txtBarcode.Text = barcodeData;
            }
        }

        int count = 3;

        private async void btnStart_Click(object sender, EventArgs e)
        {
            count = 3;

            string barcodeData = await GetBarcode();
            while (string.IsNullOrEmpty(barcodeData) && count > 0)
            {
                barcodeData = await GetBarcode();
                count--;
            }
            if (count == 0)
            {
                MessageBox.Show("Không đọc được mã vạch");
                //await ToggleLight();
            }
            else
            {
                if (!IsLightOn)
                {
                    await ToggleLight();
                }
                await Task.Delay(2000);
                txtBarcode.Text = barcodeData;

                await Task.Delay(1000);
                Bitmap capturedImage = await GetImage();
                if (capturedImage != null)
                {
                    saveImageCapture(capturedImage, barcodeData);
                    pictureBox1.Image?.Dispose();
                    pictureBox1.Image = capturedImage;
                    pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
                }
                await ToggleLight();
            }

        }

        private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            receivedData += serialPort.ReadExisting();

            txtBarcode.Invoke((MethodInvoker)(() =>
            {
                txtBarcode.Text = receivedData;
                receivedData = "";
            }));
            await ToggleLight();
            await Task.Delay(3000);
            Bitmap capturedImage = await GetImage();
            if (capturedImage != null)
            {
                saveImageCapture(capturedImage, txtBarcode.Text);
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = capturedImage;
                pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
            }
            await ToggleLight();
        }


        private void btnResetBarcode_Click(object sender, EventArgs e)
        {
            if (ws != null)
            {
                if (ws.IsAlive)
                {
                    ws.Send("5");
                }
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (ws != null)
            {
                if (ws.IsAlive)
                {
                    ws.Send("9");
                }
            }
        }

        private void txtBarcode_TextChanged(object sender, EventArgs e)
        {

        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            if (ws != null)
            {
                if (IsRs232On = true) // chuyển sang quét barcode bằng usb
                {
                    ws.Send("7");
                    IsRs232On = false;
                    RS232State?.Invoke(this, IsRs232On);
                }
                else // chuyển sang quét barcode bằng rs232
                {
                    ws.Send("8");
                    IsRs232On = true;
                    RS232State?.Invoke(this, IsRs232On);
                }
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtBarcode.Text = "";
        }

        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void checkStatus_CheckedChanged(object sender, EventArgs e)
        {
            if (checkStatus.Checked)
            {
                checkStatus.Text = "RS232";
                if (ws != null)
                {
                    if (ws.IsAlive)
                    {
                        ws.Send("4");
                    }
                }
            }
            else
            {
                checkStatus.Text = "Key Board";
                if (ws != null)
                {
                    if (ws.IsAlive)
                    {
                        ws.Send("3");
                    }
                    else
                    {

                    }
                }
            }
        }



        int quanlity // 0 - 63
        {
            get { return GetCameraProperty("quanlity"); }
            set { SetCameraProperty("quanlity", value); }
        }
        int brightness // -2 - 2
        {
            get { return GetCameraProperty("brightness"); }
            set { SetCameraProperty("brightness", value); }
        }
        int contrast // -2 - 2
        {
            get { return GetCameraProperty("contrast"); }
            set { SetCameraProperty("contrast", value); }
        } // Hàm set độ tương phản
        int saturation // -2 - 2
        {
            get { return GetCameraProperty("saturation"); }
            set { SetCameraProperty("saturation", value); }
        } // Hàm set độ bão hòa
        int setspecialeffect // 0 - 6
        {
            get { return GetCameraProperty("special_effect"); }
            set { SetCameraProperty("special_effect", value); }
        } // Hàm set hiệu ứng
        int setwhitebalance // 0 or 1
        {
            get { return GetCameraProperty("awb"); }
            set { SetCameraProperty("awb", value); }
        } // Hàm set cân bằng trắng
        int setawb_gain // 0 or 1
        {
            get { return GetCameraProperty("awb_gain"); }
            set { SetCameraProperty("awb_gain", value); }
        } // Hàm set cân bằng trắng tự động
        int set_wb_mode // 0 - 4
        {
            get { return GetCameraProperty("wb_mode"); }
            set { SetCameraProperty("wb_mode", value); }
        } // Hàm set chế độ cân bằng trắng
        int setaec_value // 0 - 1200
        {
            get { return GetCameraProperty("aec_value"); }
            set { SetCameraProperty("aec_value", value); }
        } // Hàm set điều chỉnh độ sáng
        int set_expousre_ctrl // 0 or 1
        {
            get { return GetCameraProperty("aec"); }
            set { SetCameraProperty("aec", value); }
        } // Hàm set điều chỉnh độ sáng tự động
        int setaec // 0 or 1
        {
            get { return GetCameraProperty("aec2"); }
            set { SetCameraProperty("aec2", value); }
        } // Hàm set điều chỉnh độ sáng tự động
        int set_ae_level // -2  - 2
        {
            get { return GetCameraProperty("ae_level"); }
            set { SetCameraProperty("ae_level", value); }
        } // Hàm set điều chỉnh độ sáng
        int setgain_ctrl // 0 or 1
        {
            get { return GetCameraProperty("agc"); }
            set { SetCameraProperty("agc", value); }
        } // Hàm set tăng giảm độ sáng
        int set_agc_gain
        {
            get { return GetCameraProperty("agc_gain"); }
            set { SetCameraProperty("agc_gain", value); }
        } // Hàm set tăng giảm độ sáng tự động
        int set_gainceiling // 0 - 6
        {
            get { return GetCameraProperty("gainceiling"); }
            set { SetCameraProperty("gainceiling", value); }
        } // Hàm set tăng giảm độ sáng tự động
        int set_bpc // 0 or 1
        {
            get { return GetCameraProperty("bpc"); }
            set { SetCameraProperty("bpc", value); }
        } // Hàm set bpc
        int set_wpc // 0 or 1
        {
            get { return GetCameraProperty("wpc"); }
            set { SetCameraProperty("wpc", value); }
        } // Hàm set wpc
        int set_raw_gma
        {
            get { return GetCameraProperty("raw_gma"); }
            set { SetCameraProperty("raw_gma", value); }
        } // Hàm set raw_gma
        int set_lenc // 0 or 1
        {
            get { return GetCameraProperty("lenc"); }
            set { SetCameraProperty("lenc", value); }
        } // Hàm set lenc
        int set_hmirror // 0 or 1
        {
            get { return GetCameraProperty("hmirror"); }
            set { SetCameraProperty("hmirror", value); }
        } // Hàm set hmirror
        int set_vflip // 0 or 1
        {
            get { return GetCameraProperty("vflip"); }
            set { SetCameraProperty("vflip", value); }
        } // Hàm set vflip
        int set_dcw // 0 or 1
        {
            get { return GetCameraProperty("dcw"); }
            set { SetCameraProperty("dcw", value); }
        } // Hàm set dcw
        int set_colorbar // 0 or 1 
        {
            get { return GetCameraProperty("colorbar"); }
            set { SetCameraProperty("colorbar", value); }
        } // Hàm set colorbar


    }
}
