#include "esp_camera.h"
#include <WiFi.h>
#include "GM65_scanner.h"
#include <HTTPClient.h>
#include <WebSocketServer.h>
#include <ESPAsyncWebServer.h>
#include "esp_event.h"
#include "esp_netif.h"
#include "esp_wifi.h"

#define CAMERA_MODEL_AI_THINKER // Has PSRAM

#include "camera_pins.h"

// ===========================
// Enter your WiFi credentials
// ===========================
char ssid[50]; 
const char* password = "Stargel123";
String CodeAlarm = "0";

#define MAX_STA_CONN 4

#define QR Serial1
GM65_scanner gm65_1(&QR);

#define BTN_PIN  2
#define LED_PIN  12
#define HTTP_PORT 8181

// ----------------------------------------------------------------------------
// Definition of global constants
// ----------------------------------------------------------------------------

const uint8_t DEBOUNCE_DELAY = 10; // in milliseconds

void startCameraServer();
void setupLedFlash(int pin);
void sendWebSocketMessage();
void handleButtonPress();
void initWebSocket();

// ----------------------------------------------------------------------------
// Definition of the Button component
// ----------------------------------------------------------------------------

struct Button {
    // state variables
    uint8_t  pin;
    bool     lastReading;
    uint32_t lastDebounceTime;
    uint16_t state;

    bool pressed()                { return state == 1; }
    bool released()               { return state == 0xffff; }
    bool held(uint16_t count = 0) { return state > 1 + count && state < 0xffff; }

    void read() {
        bool reading = digitalRead(pin);

        if (reading != lastReading) {
            lastDebounceTime = millis();
        }

        if (millis() - lastDebounceTime > DEBOUNCE_DELAY) {
            bool pressed = reading == LOW;
            if (pressed) {
                     if (state  < 0xfffe) state++;
                else if (state == 0xfffe) state = 2;
            } else if (state) {
                state = state == 0xffff ? 0 : 0xffff;
            }
        }

        lastReading = reading;
    }
};

// ----------------------------------------------------------------------------
// Definition of the LED component
// ----------------------------------------------------------------------------

struct Led {
    // state variables
    uint8_t pin;
    bool    on;

    // methods
    void update() {
        digitalWrite(pin, on ? HIGH : LOW);
    }
};

struct rs232 {
    // state variables
    bool    on;

    // methods
    //std::string update() {
    void update() {
      if (on) {
          QR.begin(9600, SERIAL_8N1, 1, 3);
      } else {
          QR.end();
      }
      //return on ? "RS232 On" : "RS232 Off";
    }

};


// ----------------------------------------------------------------------------
// Definition of global variables
// ----------------------------------------------------------------------------

Led    led    = { LED_PIN, false };
Button button = { BTN_PIN, HIGH, 0, 0 };
rs232  rs232Instance = { true }; 
AsyncWebServer server(HTTP_PORT);
AsyncWebSocket ws("/ws");


// ----------------------------------------------------------------------------
// WebSocket initialization
// ----------------------------------------------------------------------------

void notifyClients() {
    ws.textAll(led.on ? "on" : "off");
}

void handleWebSocketMessage(void *arg, uint8_t *data, size_t len, void *event_data) {
    AwsFrameInfo *info = (AwsFrameInfo*)arg;
    if (info->final && info->index == 0 && info->len == len && info->opcode == WS_TEXT) {
        data[len] = 0;
        int cmd = atoi((char*)data);
        switch(cmd)
        {
          case 0: // reset alarm
            CodeAlarm = "0";
            break;
          case 1://led on
            if (led.on == false){
              led.on = true;
              led.update();
            }
            CodeAlarm = "1";
            //notifyClients("1");
            break;
          case 2: // led off
            if (led.on == true){
              led.on = false;
              led.update();
            }
            CodeAlarm = "2";
            //notifyClients("2");
            break;
          case 3: // switch rs232 -> usb barcode
            gm65_1.set_working_mode(0);
            if (!gm65_1.set_usb_output())
            {
              CodeAlarm = "3";
            }else {
              CodeAlarm = "4";
            }
            if (QR)
            {
              QR.end();
            }
            break;
          case 4: // switch usb -> rs232
            if (!QR)
            {
              QR.begin(9600, SERIAL_8N1, 1, 3);
            }
            gm65_1.set_working_mode(1);
            if (!gm65_1.set_serial_output())
            {
              CodeAlarm = "5";
            }
            else {
              CodeAlarm = "6";
            }
            //notifyClients("4");
            break;
          case 5: // reset barcode
            if (!QR) QR.begin(9600, SERIAL_8N1, 1, 3);
            if (!gm65_1.init()){
              CodeAlarm = "7";
            }
            else {
              CodeAlarm = "8";
            }
            //notifyClients("5");
            break;
          case 6: // reset cam
            CodeAlarm = "9";
            ESP.restart();
            //notifyClients("6");
            break;
          case 7: // rs232 off
            if (rs232Instance.on == true)
            {
              rs232Instance.on = false;
              rs232Instance.update();
            }
            CodeAlarm = "10";
            break;
          case 8: // rs232 on
            if (rs232Instance.on == false)
            {
              rs232Instance.on = true;
              rs232Instance.update();
            }
            CodeAlarm = "11";
            break;
          case 9: // reset Camera
            initCamera();
            CodeAlarm = "12";
            break;
        }
        
        notifyError(CodeAlarm);

    }
}

        
        // if (strcmp((char*)data, "toggle") == 0) {
        //     led.on = !led.on;
        //     led.update();
        //     notifyClients();
        // }

        // if (strcmp((char*)data, "mac") == 0) {
        //     char message[100]; 
        //     wifi_event_ap_staconnected_t* event = (wifi_event_ap_staconnected_t*)event_data;
        //     snprintf(message, sizeof(message), MACSTR, MAC2STR(event->mac));
        //     notifyError(message);
        // }

        // if (strcmp((char*)data, "connect") == 0) {
        //     char message[100]; 
        //     wifi_event_ap_staconnected_t* event = (wifi_event_ap_staconnected_t*)event_data;
        //     snprintf(message, sizeof(message), MACSTR, MAC2STR(event->mac));
        //     notifyError(message);
        // }
        // if (strcmp((char*)data, "connect_gm65") == 0) {
        //     int gm65Version = gm65_1.getSoftwareVersion();
        //     if (gm65Version != 0x00) {
        //         char responseMessage[100];
        //         snprintf(responseMessage, sizeof(responseMessage), "Connected to GM65 Version: 0x%02X", gm65Version);
        //         ws.textAll(responseMessage);
        //     } else {
        //         ws.textAll("Failed to connect to GM65");
        //     }
        // }
        // if (strcmp((char *)data, "rs232") == 0) {
        //     rs232Instance.on = !rs232Instance.on;
        //     std::string message = rs232Instance.update();
        //     notifyError(message.c_str()); 
        // }

        // if (strcmp((char *)data, "1: Command Triggered Mode") == 0) {
        //     gm65_1.set_working_mode(1);
        //     notifyError("1"); 
        // }

        // if (strcmp((char *)data, "2: Manual Mode") == 0) {
        //     gm65_1.set_working_mode(0);
        //     //notifyError(message.c_str()); 
        // }

      

        // if (strcmp((char *)data, "1: RS232") == 0) {
        //     gm65_1.set_serial_output();
        //     //notifyError(message.c_str()); 
        // }

        // if (strcmp((char *)data, "2: USB") == 0) {
        //     gm65_1.set_virtual_serial_port();
        //     //notifyError(message.c_str()); 
        // }

        // if (strcmp((char *)data, "ResetBarcode") == 0) {
        //     gm65_1.init();
        //     //notifyError(message.c_str()); 
        // }

        // if (strcmp((char *)data, "ResetCam") == 0) {
        //     ESP.restart();
        //     //notifyError(message.c_str()); 
        // }

void onEvent(AsyncWebSocket       *server,
             AsyncWebSocketClient *client,
             AwsEventType          type,
             void                 *arg,
             uint8_t              *data,
             size_t                len) {

    switch (type) {
        case WS_EVT_CONNECT:
            //Serial.printf("WebSocket client #%u connected from %s\n", client->id(), client->remoteIP().toString().c_str());
            notifyError("13");
            break;
        case WS_EVT_DISCONNECT:
            //Serial.printf("WebSocket client #%u disconnected\n", client->id());
            notifyError("14");
            break;
        case WS_EVT_DATA:
            handleWebSocketMessage(arg, data, len, NULL);
            break;
        case WS_EVT_PONG:
            notifyError("15");
            break;
        case WS_EVT_ERROR:
            break;
    }
}

void notifyError(String message) {
    ws.textAll(message);
}

void initWebSocket() {
    server.begin();
    ws.onEvent(onEvent);
    server.addHandler(&ws);
}

// void wifi_event_handler(void* arg, esp_event_base_t event_base, int32_t event_id, void* event_data) {
//     char message[100]; 

//     if (event_id == WIFI_EVENT_AP_STACONNECTED) {
//         wifi_event_ap_staconnected_t* event = (wifi_event_ap_staconnected_t*)event_data;
//         snprintf(message, sizeof(message), MACSTR, MAC2STR(event->mac));
//         notifyError(message);
//     } else if (event_id == WIFI_EVENT_AP_STADISCONNECTED) {
//         wifi_event_ap_stadisconnected_t* event = (wifi_event_ap_stadisconnected_t*)event_data;
//         ESP.restart();
//     } 
// }

void initCamera(){
  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sscb_sda = SIOD_GPIO_NUM;
  config.pin_sscb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG; //YUV422,GRAYSCALE,RGB565,JPEG

  // Select lower framesize if the camera doesn't support PSRAM
  if(config.pixel_format == PIXFORMAT_JPEG){
    if(psramFound()){
      config.frame_size = FRAMESIZE_VGA;
      config.jpeg_quality = 4;
      config.fb_count = 2;
      config.grab_mode = CAMERA_GRAB_LATEST;
    } else {
      // Limit the frame size when PSRAM is not available
      config.frame_size = FRAMESIZE_SVGA;
      config.fb_location = CAMERA_FB_IN_DRAM;
    }
  } else {
    // Best option for face detection/recognition
    config.frame_size = FRAMESIZE_240X240;
#if CONFIG_IDF_TARGET_ESP32S3
    config.fb_count = 2;
#endif
  }
  
  // Initialize the Camera
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK) {
    notifyError("16");
    return;
  }

  sensor_t * s = esp_camera_sensor_get();

  s->set_brightness(s, 0);     // -2 to 2
  s->set_contrast(s, 0);       // -2 to 2
  s->set_saturation(s, 0);     // -2 to 2
  s->set_sharpness(s, 0);
  s->set_special_effect(s, 0); // 0 to 6 (0 - No Effect, 1 - Negative, 2 - Grayscale, 3 - Red Tint, 4 - Green Tint, 5 - Blue Tint, 6 - Sepia)
  s->set_whitebal(s, 1);       // 0 = disable , 1 = enable
  s->set_awb_gain(s, 1);       // 0 = disable , 1 = enable
  s->set_wb_mode(s, 0);        // 0 to 4 - if awb_gain enabled (0 - Auto, 1 - Sunny, 2 - Cloudy, 3 - Office, 4 - Home)
  s->set_exposure_ctrl(s, 0);  // 0 = disable , 1 = enable
  s->set_aec2(s, 1);           // 0 = disable , 1 = enable
  s->set_ae_level(s, 0);       // -2 to 2
  s->set_aec_value(s, 493);    // 0 to 1200
  s->set_gain_ctrl(s, 1);      // 0 = disable , 1 = enable
  s->set_agc_gain(s, 0);       // 0 to 30
  s->set_gainceiling(s, (gainceiling_t)0);  // 0 to 6
  s->set_bpc(s, 0);            // 0 = disable , 1 = enable
  s->set_wpc(s, 1);            // 0 = disable , 1 = enable
  s->set_raw_gma(s, 1);        // 0 = disable , 1 = enable
  s->set_lenc(s, 0);           // 0 = disable , 1 = enable
  s->set_hmirror(s, 0);        // 0 = disable , 1 = enable
  s->set_vflip(s, 0);          // 0 = disable , 1 = enable
  s->set_dcw(s, 1);            // 0 = disable , 1 = enable
  s->set_colorbar(s, 0);       // 0 = disable , 1 = enable 
}

void setup() {
  String macAddress = WiFi.macAddress();

  int sum = 0;
  for (int i = 0; i < macAddress.length(); i++) {
    sum += macAddress[i] - '0'; 
  }

  snprintf(ssid, sizeof(ssid), "Stargel %d", sum);
  
  

  WiFi.mode(WIFI_AP);
  IPAddress Ip(192, 168, 127, 1);
  IPAddress NMask(255, 255, 255, 0);
  WiFi.softAPConfig(Ip, Ip, NMask);
  WiFi.softAP(ssid, password);
  delay(5000);

  //esp_event_handler_register(WIFI_EVENT, ESP_EVENT_ANY_ID, &wifi_event_handler, NULL);

  initWebSocket();
  initCamera();

  startCameraServer();

  //QR.begin(9600, SERIAL_8N1, 1, 3);
  gm65_1.init();
  gm65_1.set_working_mode(0);
  gm65_1.set_usb_output();

  pinMode(button.pin, INPUT);
  pinMode(led.pin, OUTPUT);

}

void loop() {
  ws.cleanupClients();
}


