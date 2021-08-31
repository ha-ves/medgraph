/*
 Name:		ECG_6_LEAD.ino
 Created:	8/31/2021 2:22:43 PM
 Author:	HaveS
*/
unsigned int lead_1 = 0;
unsigned int lead_2 = 0;
unsigned int lead_3 = 0;
unsigned int aVR = 0;
unsigned int aVL = 0;
unsigned int aVF = 0;
const int btn = 11;
int selector = 0;
boolean isPressed = false;

#define waktubpm     100
const int numReadings1 = 15;
int readings1[numReadings1];      // the readings from the analog input
int readIndex1 = 0;              // the index of the current reading
int total1 = 0;                  // the running total
unsigned int sinyal;
unsigned long waktuBPM, waktuawal = 0;
float ref, hold, BPMpalsu, ppg;
int BPM, pulse;
int b = 0;
uint32_t tsLastReport = 0;
String data;

unsigned long lastmil = 0;

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(115200);
	pinMode(A0, INPUT);// put your setup code here, to run once:
	pinMode(A1, INPUT);
	pinMode(A2, INPUT);
	pinMode(A3, INPUT);
	pinMode(A4, INPUT);
	pinMode(A5, INPUT);
	pinMode(7, OUTPUT);
	pinMode(8, OUTPUT);
	pinMode(12, OUTPUT);
	pinMode(13, OUTPUT);
}

// the loop function runs over and over again until power down or reset
void loop() {
	if (digitalRead(btn) == LOW && isPressed == false) //button is pressed AND this is the first digitalRead() that the button is pressed
	{
		isPressed = true;  //set to true, so this code will not run again until button released
		doSwitchStatement(); // a call to a separate function that performs the switch statement and subsequent evoked code

		selector++; // this is done after the doSwitchStatement(), so case 0 will be executed on the first button press 
		if (selector > 1) {
			selector = 0;
		}
		// selector = (selector+1) % 4;  // does the same, without if-statement
	}
	else if (digitalRead(btn) == HIGH)
	{
		isPressed = false; //button is released, variable reset
	}

	perhitunganbpm();
	doSwitchStatement();
	lead_1 = analogRead(A0);
	lead_2 = analogRead(A1);
	lead_3 = analogRead(A2);
	aVR = analogRead(A3);
	aVL = analogRead(A4);
	aVF = analogRead(A5);

	if (millis() - lastmil >= 33) {
		lastmil = millis();
		//KIRIM PAKET 
		data += "*,";
		data += selector;
		data += ',';
		data += lead_1;
		data += ',';
		data += lead_2;
		data += ',';
		data += lead_3;
		data += ',';
		data += aVR;
		data += ',';
		data += aVL;
		data += ',';
		data += aVF;
		data += "#\r";
		Serial.print(data);

		data = "";
	}
}

void doSwitchStatement() {
	switch (selector) {
	case 0:
		digitalWrite(7, LOW); // bagian 1 
		digitalWrite(8, LOW);
		digitalWrite(12, LOW);
		digitalWrite(13, HIGH);
		break;
	case 1:
		digitalWrite(7, HIGH);// bagian 2
		digitalWrite(8, LOW);
		digitalWrite(12, HIGH);
		digitalWrite(13, HIGH);
		break;
	}
}

void perhitunganbpm()
{
	if (millis() - tsLastReport > waktubpm)
	{
		sinyal = analogRead(A2);
		total1 = total1 - readings1[readIndex1];
		readings1[readIndex1] = sinyal;
		total1 = total1 + readings1[readIndex1];
		readIndex1 = readIndex1 + 1;
		if (readIndex1 >= numReadings1) {
			readIndex1 = 0;
		}
		pulse = total1 / numReadings1;//mengambiil grafik ppg
		ppg = pulse;
		if (ref <= ppg) { ref = ppg; }
		else { ref = ref; hold = (ref * 0.9); }
		waktuawal = millis() - waktuBPM;
		if (ppg > hold)
		{
			b = 1;
		}
		if (ppg < (hold * 0.9))
		{
			if (b == 1) {
				BPMpalsu++;
				hold = 0;
				b = 0;
			}
		}
		if (BPMpalsu == 3) {
			BPM = 180000 / waktuawal;
			BPMpalsu = 0;

			waktuBPM = millis();
		}

	}
}