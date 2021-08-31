/*
 Name:		TENSI_METER.ino
 Created:	8/31/2021 2:45:04 PM
 Author:	HaveS
*/
#include <SoftwareSerial.h>

SoftwareSerial mySerial(10, 11); // RX, TX

#include <Wire.h> // i2c library
#include <LiquidCrystal_I2C.h> // lcd library

LiquidCrystal_I2C lcd(0x27, 16, 2); // konfigurasi object lcd

// konfigurasi pin i/o
#define mpx       A0
#define button    A1
#define motor     2
#define selenoid  3

// global variable
int sys = 0, dia = 0, cnt = 0, cnt1 = 0, cnt11 = 0;
int xsys = 0, xdia = 0;
int lsys;
int flag = 0;
unsigned long previousMillis = 0;
unsigned long previousMillis1 = 0;
unsigned long previousMillis2 = 0;

// the setup function runs once when you press reset or power the board
void setup() {
    mySerial.begin(115200);
    Serial.begin(115200);
    // inisialisasi lcd
    lcd.begin();
    // inisialisasi i/o
    pinMode(button, INPUT_PULLUP);
    pinMode(motor, OUTPUT);
    pinMode(selenoid, OUTPUT);
    // lcd tampilan awal
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("TENSI METER");
    delay(1000);
    Serial.println("Ready");
}

// the loop function runs over and over again until power down or reset
void loop() {
    // jalankan fungtion runprogram
    runprogram();
}

// program utama
void runprogram() {

    // pembacaan sensor
    int adc = analogRead(A0); // bada data sensor adc
    float v = (float)adc * (5.0 / 1023.0); // konversi ke volt
    float kpa = ((v / 5.0) - 0.04) / 0.0012858; // konversi ke kps
    //Serial.println(kpa);
    kpa = kpa - 94.33; // kalibrasi ke 0 
    if (kpa < 0)kpa = 0; // limit
    int mmhg = kpa * 7.50062; // konversi ke mmhg
    //Serial.println(mmhg);  
    // jalankan timer
    unsigned long currentMillis = millis();

    // timer update display lcd
    if (currentMillis - previousMillis >= 500) {
        previousMillis = currentMillis;

        lcd.clear();
        lcd.setCursor(0, 0);
        lcd.print("Press: ");
        lcd.print(mmhg);
        lcd.print("mmhg");
        lcd.setCursor(0, 1);
        lcd.print("Sis:");
        lcd.print(sys);
        lcd.setCursor(8, 1);
        lcd.print("Dia:");
        lcd.print(dia);
    }

    if (currentMillis - previousMillis1 >= 1000) {
        previousMillis1 = currentMillis;

        if (flag == 1) {

            if (sys == 0) {
                if (cnt1 == 2)sys = mmhg;

            }
            else {
                if (cnt1 == 0)dia = mmhg;
            }
            Serial.print("mmhg:");
            Serial.print(mmhg);
            Serial.print(" Lmmhg:");
            Serial.print(lsys);
            Serial.print(" Denyut:");
            Serial.println(cnt1);
            cnt1 = 0;

            mySerial.print("T,");
            mySerial.print(sys);
            mySerial.print(',');
            mySerial.print(dia);
            mySerial.print("#\r");
        }
    }

    //    if(currentMillis - previousMillis2 >= 33){
    //      previousMillis2 = currentMillis;
    //
    //      mySerial.print("M,");
    //      mySerial.print(mmhg);
    //      mySerial.print("#\r");
    //    }

       // batas pompa dan off motor
    if (mmhg >= 180) {
        lsys = mmhg;
        digitalWrite(motor, 0);
        analogWrite(selenoid, 255); // selenoid on 
        flag = 1;
    }

    // batas terkecil dan off selemoid
    if (flag == 1 && mmhg < 40) {
        analogWrite(selenoid, 0);
        flag = 0;
    }
    // jika sistolik dan diastolik nilainya sdh di dapat selenoid off
    if (sys > 0 && dia > 0 && flag == 1) {
        analogWrite(selenoid, 0);
        flag = 0;

    }

    // membandingkan tekanan saat ini dan tadi
    if (flag == 1) {
        if (lsys > mmhg) {
            lsys = mmhg;
            if (cnt == 0) {
                cnt = 1;
                cnt1++; // count perbedaan
            }
        }
        else {
            cnt = 0;
        }

    }
    // tekan tombol start
    if (digitalRead(button) == 0 && flag == 0) {
        digitalWrite(motor, 1); // motor on
        analogWrite(selenoid, 255); // selenoid on 
        sys = 0;
        dia = 0;
        cnt = 0;
        cnt1 = 0;
        cnt11 = 0;
    }

}