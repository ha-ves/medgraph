/*
 Name:		ESP8266_MainAP.ino
 Created:	8/31/2021 2:16:49 PM
 Author:	HaveS
*/
#include <ESP8266WiFi.h>
#include <SoftwareSerial.h>
extern "C" {
#include<user_interface.h>
}

// Set AP credentials
#define AP_SSID "Tensio-821657"
#define AP_PASS "821657mmHG"

#define server_port 12727
#define alat_port 9128

WiFiServer server(server_port);
WiFiServer alat_server(alat_port);

#define MAX_ALAT 2
WiFiClient* theAlat[MAX_ALAT] = { new WiFiClient(), new WiFiClient() };
WiFiClient* theClient = new WiFiClient();
WiFiClient checkClient;

String rxBuf = "";
char charBuf[100];
bool allconnect = false;

SoftwareSerial swSer(D1, D2, false);

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(115200);
	swSer.begin(115200);
	Serial.setTimeout(5);
	swSer.setTimeout(5);

	// Begin Access Point
	WiFi.mode(WIFI_AP);
	WiFi.softAP(AP_SSID, AP_PASS);

	Serial.println();
	Serial.print("AP : IP = ");
	Serial.print(AP_SSID);
	Serial.print(" : ");
	Serial.println(WiFi.softAPIP());

	server.begin();
	Serial.print("Bridge Port : [");
	Serial.print(server_port);
	Serial.println(']');

	alat_server.begin();
	Serial.print("Relayer Port : [");
	Serial.print(alat_port);
	Serial.println(']');
}

// the loop function runs over and over again until power down or reset
void loop() {
	checkClient = server.available();
	if (checkClient && !theClient->connected()) {
		theClient = new WiFiClient(checkClient);
		theClient->setTimeout(5);
		theClient->setNoDelay(true);
		theClient->keepAlive(5, 3, 5);
		Serial.print("New Client Connected : ");
		Serial.println(theClient->remoteIP());
	}

	checkClient = alat_server.available();
	if (checkClient) {
		for (int x = 0; x < MAX_ALAT; x++) {
			if (!theAlat[x]->connected()) {
				theAlat[x] = new WiFiClient(checkClient);
				theAlat[x]->setTimeout(5);
				theAlat[x]->setNoDelay(true);
				theAlat[x]->keepAlive(5, 3, 5);
				Serial.print("New Alat Connected : ");
				Serial.println(theAlat[x]->remoteIP());
				break;
			}
		}
	}

	if (theClient->connected()) {
		RunBridgeLoop();
	}
}

void RunBridgeLoop() {
	for (int x = 0; x < MAX_ALAT; x++) {
		if (theAlat[x]->available()) {
			rxBuf = theAlat[x]->readStringUntil('#');
			theClient->print(String(rxBuf + '#'));
		}
	}
	if (swSer.available()) {
		rxBuf = swSer.readStringUntil('\r');
		if (rxBuf.endsWith("#")) theClient->print(rxBuf);
	}
	if (Serial.available()) {
		rxBuf = Serial.readStringUntil('\r');
		if (rxBuf.endsWith("#")) theClient->print(rxBuf);
	}
}