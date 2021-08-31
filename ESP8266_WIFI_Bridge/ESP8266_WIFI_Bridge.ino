/*
 Name:		ESP8266_WIFI_Bridge.ino
 Created:	8/31/2021 2:47:09 PM
 Author:	HaveS
*/
#include <ESP8266WiFi.h>

#define STASSID "Tensio-821657"
#define STAPSK  "821657mmHG"

const char* ssid = STASSID;
const char* password = STAPSK;

//#define SPO2

#ifndef SPO2
#define ECG
#endif

#ifdef SPO2
IPAddress staticIP(192, 168, 4, 5); //ESP8266 static ip
#endif
#ifdef ECG
IPAddress staticIP(192, 168, 4, 6); //ESP8266 static ip
#endif
IPAddress ap_gateway(192, 168, 4, 1); //IP Address of your WiFi Router (Gateway)
IPAddress subnet(255, 255, 255, 0); //Subnet mask

#define port 9128
WiFiClient theAP;

String rxBuf = "";

// the setup function runs once when you press reset or power the board
void setup() {
    Serial.begin(115200);
    theAP.setNoDelay(true);
    Serial.setTimeout(10);

    ConnectWiFi();
    ConnectServer();
}

// the loop function runs over and over again until power down or reset
void loop() {
    while (theAP.connected()) { RunBridgeLoop(); }
    Serial.println("Disconnected");
    delay(3000);
    if (WiFi.status() != WL_CONNECTED) ConnectWiFi();
    else if (!theAP.connected()) ConnectServer();
}

void ConnectWiFi() {
    //We start by connecting to a WiFi network
    Serial.println();
    Serial.print("Connecting to ");
    Serial.println(ssid);

    WiFi.config(staticIP, subnet, ap_gateway, NULL);
    WiFi.mode(WIFI_STA);
    WiFi.begin(ssid, password);

    while (WiFi.status() != WL_CONNECTED) {
        delay(500);
        Serial.print(".");
    }

    ap_gateway = WiFi.gatewayIP();

    Serial.println("");
    Serial.println("WiFi connected");
    Serial.println("IP address: ");
    Serial.println(WiFi.localIP());
}

void ConnectServer() {
    Serial.print("connecting to ");
    Serial.print(ap_gateway);
    Serial.print(':');
    Serial.println(port);

    // Use WiFiClient class to create TCP connections
    while (!theAP.connect(ap_gateway, port)) {
        Serial.println("connection failed");
        delay(3000);
    }

    Serial.println("Connected to Alat Server");
}

void RunBridgeLoop() {
    if (Serial.available()) {
        rxBuf = Serial.readStringUntil('#');
        theAP.print(String(rxBuf + '#'));
    }
}