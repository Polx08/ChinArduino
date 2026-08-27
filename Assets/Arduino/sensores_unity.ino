#include <SPI.h>
#include <MFRC522.h>
#include <Adafruit_NeoPixel.h>

#define SS_PIN 10
#define RST_PIN 9
#define LED_PIN 6
#define LED_COUNT 12
#define TRIG_PIN 7
#define ECHO_PIN 8
#define INTERVALO_DISTANCIA 100

MFRC522 mfrc522(SS_PIN, RST_PIN);
Adafruit_NeoPixel anillo(LED_COUNT, LED_PIN, NEO_GRB + NEO_KHZ800);

String bufferSerial = "";
unsigned long ultimaMedicionDistancia = 0;

void setup() {
  Serial.begin(9600);
  SPI.begin();
  mfrc522.PCD_Init();

  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  anillo.begin();
  anillo.show();
}

void loop() {
  leerRFID();
  leerDistancia();
  leerComandosSerial();
}

void leerRFID() {
  if (!mfrc522.PICC_IsNewCardPresent()) {
    return;
  }

  if (!mfrc522.PICC_ReadCardSerial()) {
    return;
  }

  String uid = "";
  for (byte i = 0; i < mfrc522.uid.size; i++) {
    if (mfrc522.uid.uidByte[i] < 0x10) {
      uid += "0";
    }
    uid += String(mfrc522.uid.uidByte[i], HEX);
  }
  uid.toUpperCase();
  Serial.println(uid);

  mfrc522.PICC_HaltA();
  mfrc522.PCD_StopCrypto1();
}

void leerDistancia() {
  unsigned long ahora = millis();

  if (ahora - ultimaMedicionDistancia < INTERVALO_DISTANCIA) {
    return;
  }

  ultimaMedicionDistancia = ahora;

  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);

  long duracion = pulseIn(ECHO_PIN, HIGH, 30000);

  if (duracion > 0) {
    float distanciaCm = duracion * 0.0343 / 2;
    Serial.println(distanciaCm);
  }
}

void leerComandosSerial() {
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      procesarComandoLed(bufferSerial);
      bufferSerial = "";
    }
    else if (c != '\r') {
      bufferSerial += c;
    }
  }
}

void procesarComandoLed(String comando) {
  if (!comando.startsWith("LED:")) {
    return;
  }

  int primerosDosPuntos = comando.indexOf(':', 4);
  if (primerosDosPuntos == -1) {
    return;
  }

  String indicesStr = comando.substring(4, primerosDosPuntos);
  String colorHex = comando.substring(primerosDosPuntos + 1);

  long colorLong = strtol(colorHex.c_str(), NULL, 16);
  byte r = (colorLong >> 16) & 0xFF;
  byte g = (colorLong >> 8) & 0xFF;
  byte b = colorLong & 0xFF;

  anillo.clear();

  int inicio = 0;
  int fin = indicesStr.indexOf(',');

  while (true) {
    String indiceStr = (fin == -1) ? indicesStr.substring(inicio) : indicesStr.substring(inicio, fin);
    int indice = indiceStr.toInt();

    if (indice >= 0 && indice < LED_COUNT) {
      anillo.setPixelColor(indice, anillo.Color(r, g, b));
    }

    if (fin == -1) {
      break;
    }

    inicio = fin + 1;
    fin = indicesStr.indexOf(',', inicio);
  }

  anillo.show();
}
