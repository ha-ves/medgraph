/*
 Name:		SPO2.ino
 Created:	8/31/2021 2:31:19 PM
 Author:	HaveS
*/
#define waktuSPO 100
#define waktubpm     1000
uint32_t tsLastReport = 0;
uint32_t tsLastReport0 = 0;

#define USE_ARDUINO_INTERRUPTS true    // Set-up low-level interrupts for most acurate BPM math.
#include <PulseSensorPlayground.h>     // Includes the PulseSensorPlayground Library.   

//  Variables
const int PulseWire = A4;       // PulseSensor PURPLE WIRE connected to ANALOG PIN 0         // The on-board Arduino LED, close to PIN 13.
int Threshold = 550;
int myBPM;// Determine which Signal to "count as a beat" and which to ignore.
PulseSensorPlayground pulseSensor;
int spotok;
float t1;
const long interval = 5000;
unsigned long previousMillis = 0, lastmil = 0;
unsigned char tampilkan = 0;
unsigned char spo2 = 0;
int waktu_suhu = 0;
unsigned int maksimumACredlamp = 0, maksimumACinfrared = 0;;
unsigned char counteran = 0;
int logika, holdACinfrared;
float ratio = 0;
float bagi1, bagi2;
int nodetak = 0;
unsigned char cekdetak = 0;
String data;

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(115200);
	pulseSensor.begin();
	pulseSensor.analogInput(PulseWire);         //auto-magically blink Arduino's LED with heartbeat.
	pulseSensor.setThreshold(Threshold);
}

// the loop function runs over and over again until power down or reset
void loop() {
    if (millis() - tsLastReport > waktuSPO)
    {
        float ACredlamp = analogRead(A1); //pembacaan sinyal ACRed
        float ACredlamp1 = (ACredlamp / 1023) * 5;
        float ACinfrared = analogRead(A0); //pembacaan sinyal ACIR
        float ACinfrared1 = (ACinfrared / 1023) * 5;
        float DCredlamp = analogRead(A2); //pembacaan sinyal DCRed
        float DCredlamp1 = (DCredlamp / 1023) * 5;
        float DCinfrared = analogRead(A3); //pembacaan sinyal DCIR
        float DCinfrared1 = (DCinfrared / 1023) * 5;
        //====olah SPO2====//
        if (maksimumACredlamp < ACredlamp)
        {
            maksimumACredlamp = ACredlamp;

        }      //untuk mendeteksi puncak (apabila puncak kurang dari maksimum puncak tetep menampilkan nilai puncak tertinggi)
        if (maksimumACinfrared < ACinfrared)
        {
            maksimumACinfrared = ACinfrared;
        }
        else
        {
            maksimumACinfrared = maksimumACinfrared; ////nilai maks acred
            holdACinfrared = (maksimumACinfrared * 0.4);
        }
        if (ACinfrared > holdACinfrared)            //apabila nilai adc dari tegangan ac infrared lebih dari 50, dan terdapat perubahan logika dari 0 ke 1 maka nilai counteran akan bertambah
        { // apabila tidak terdapat perubahan logika, counteran tidak bertambah
            if (logika == 0)
            {
                counteran++;
                nodetak = 0;
            }
            logika = 1;
        }
        else
        {
            logika = 0;
        }
        if (counteran == 5)
        {
            if (DCredlamp == 0) //apabila counteran sudah sejumlah 5 kali, dan apabila nilai adc yg dibaca dari tegangan dc red lamp = 0, maka variabel bagi 1 dianggap bernilai 0
            {
                bagi1 = 0;
            }
            else               //apabila nilai adc dari tegangan dc red lamp bukan 0, maka akan menjalankan program variabel bagi1 yg berasal dari nilai adc tegangan maksimum ac red lamp dibagi dengan nilai adc tegangan dc red lamp
            {
                bagi1 = (float)maksimumACredlamp / DCredlamp;
            }
            if (DCinfrared == 0)
            {
                bagi2 = 0;        //apabila counteran sudah sejumlah 5 kali, dan apabila nilai adc yg dibaca dari tegangan dc infrared = 0, maka variabel bagi 2 dianggap bernilai 0
            }
            else
            {
                bagi2 = (float)maksimumACinfrared / DCinfrared; //apabila nilai adc dari tegangan dc infared bukan 0, maka akan menjalankan program variabel bagi2 yg berasal dari nilai adc tegangan maksimum ac infrared dibagi dengan nilai adc tegangan dc infrared
            }

            if (bagi2 == 1) //apabila nilai adc yg dihasilkan bagi2 = 0, maka akan ditampilkan presentase nilai spo2 = 0
            {
                spo2 = spo2;
            }
            else         //apabila nilai adc yg dihasilkan bagi2 bukan 0, maka akan menjalankan program dengan variabel ratio yg menghasilkan nilai adc dari nilai adc bagi 1 dibagi dengan nilai adc bagi 2
            { //kemudian menjalankan program dengan variabel spo2 yang nilainya didapat dari perhitungan matematis dengan memasukkan nilai adc dari variabel ratioratio=(float) bagi1/bagi2;
                ratio = (float)bagi1 / bagi2;
                spo2 = (120.0 - (25.0 * ratio));
            }
            counteran = 0;              //apabila semua program diatas sudah dijalankan maka program kembali ke langkah awal/me-reset ulang program
            maksimumACredlamp = 0;
            maksimumACinfrared = 0;
        }
        tampilkan++;
        cekdetak++;

        if (cekdetak == 10 && nodetak == 0) //apabila selama 50x10ms=500ms variabel nodetak=1, maka tampilan nilai presentase spo2 = 0 dan cek detak kembali ke 0/me-reset ulang timer
        {
            spo2 = spo2;
            nodetak = 0;
            cekdetak = 0;
        }

        if (DCinfrared == 0)
        {
            spo2 = 0;
        }
        tsLastReport = millis();
    }

    if (millis() - tsLastReport0 > waktubpm)
    {
        myBPM = pulseSensor.getBeatsPerMinute();
    }

    if (millis() - lastmil > 33) {
        lastmil = millis();
        //KIRIM PAKET 
        data += "N,";
        data += analogRead(A4);
        data += ',';
        data += spo2;
        data += ',';
        data += myBPM;
        data += "#\r";
        Serial.print(data);

        data = "";
    }
}
