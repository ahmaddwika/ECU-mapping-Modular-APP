#include <Arduino.h>
#include <string.h>
#include "config.h"
#include "protocol.h"
#include "crc8.h"
#include "kline.h"

#ifndef LED_BUILTIN
#define LED_BUILTIN 2
#endif

#define FIRMWARE_VERSION "0.1.0"

uint8_t rx_frame[FRAME_MAX_PAYLOAD + 4];
size_t rx_len = 0;
uint8_t resp[FRAME_MAX_PAYLOAD + 4];

void send_response(cmd_t cmd, const uint8_t* payload, size_t payload_len) {
    if (payload_len > FRAME_MAX_PAYLOAD) {
        payload_len = FRAME_MAX_PAYLOAD;
    }
    size_t n = frame_build(resp, cmd, payload, payload_len);
    Serial.write(resp, n);
}

void handle_frame(cmd_t cmd, const uint8_t* payload, size_t payload_len) {
    digitalWrite(LED_BUILTIN, !digitalRead(LED_BUILTIN));

    switch (cmd) {
        case CMD_PING: {
            char v[24];
            int n = snprintf(v, sizeof(v), "HondaBridge %s", FIRMWARE_VERSION);
            send_response(CMD_PING, (const uint8_t*)v, (size_t)n);
            break;
        }
        case CMD_LOOPBACK:
            send_response(CMD_LOOPBACK, payload, payload_len);
            break;
        case CMD_KLINE_INIT: {
            uint8_t st = ST_OK;
            if (payload_len >= 2) {
                uint32_t baud = (uint32_t)payload[0] | ((uint32_t)payload[1] << 8);
                kline_config_t cfg;
                cfg.baud = baud;
                cfg.rx_pin = KLINE_RX_PIN;
                cfg.tx_pin = KLINE_TX_PIN;
                kline_begin(&cfg);
            } else {
                st = ST_BAD_FRAME;
            }
            send_response(CMD_KLINE_INIT, &st, 1);
            break;
        }
        case CMD_KLINE_SEND: {
            uint8_t out[FRAME_MAX_PAYLOAD + 1];
            if (payload_len < 1) {
                out[0] = ST_BAD_FRAME;
                send_response(CMD_KLINE_SEND, out, 1);
                break;
            }
            uint8_t timeout_ms = payload[0];
            uint8_t reply[FRAME_MAX_PAYLOAD];
            int got = kline_transceive(&payload[1], payload_len - 1,
                                       reply, sizeof(reply), timeout_ms);
            if (got < 0) {
                out[0] = ST_NO_ECU;
                send_response(CMD_KLINE_SEND, out, 1);
            } else if (got == 0) {
                out[0] = ST_TIMEOUT;
                send_response(CMD_KLINE_SEND, out, 1);
            } else {
                if (got > FRAME_MAX_PAYLOAD) {
                    got = FRAME_MAX_PAYLOAD;
                }
                out[0] = ST_OK;
                memcpy(&out[1], reply, (size_t)got);
                send_response(CMD_KLINE_SEND, out, (size_t)got + 1);
            }
            break;
        }
        case CMD_ECU_ID:
        case CMD_SET_MODE:
        case CMD_READ_FLASH:
        case CMD_WRITE_FLASH:
        case CMD_CHECKSUM:
        case CMD_POWER_CYCLE: {
            uint8_t st = ST_NOT_IMPL;
            send_response(cmd, &st, 1);
            break;
        }
        case CMD_MODULE_INFO: {
            const char* info = "HondaBridge 0.1.0; intf=kline; mode=bridge";
            send_response(CMD_MODULE_INFO, (const uint8_t*)info, strlen(info));
            break;
        }
        default: {
            uint8_t st = ST_UNKNOWN;
            send_response(CMD_ERROR, &st, 1);
            break;
        }
    }
}

void feed_parser(uint8_t b) {
    if (rx_len == 0) {
        if (b == FRAME_STX) {
            rx_frame[0] = b;
            rx_len = 1;
        }
        return;
    }
    if (rx_len == 1) {
        if (b == 0 || b > FRAME_MAX_PAYLOAD) {
            rx_len = 0;
            return;
        }
        rx_frame[1] = b;
        rx_len = 2;
        return;
    }

    size_t expected = 3 + rx_frame[1] + 1;
    rx_frame[rx_len++] = b;

    if (rx_len == expected) {
        size_t payload_len = rx_frame[1];
        uint8_t crc = crc8_bytes(&rx_frame[1], payload_len + 2);
        uint8_t recv_crc = rx_frame[expected - 1];
        cmd_t cmd = (cmd_t)rx_frame[2];
        rx_len = 0;
        if (crc == recv_crc) {
            handle_frame(cmd, &rx_frame[3], payload_len);
        } else {
            uint8_t st = ST_BAD_FRAME;
            send_response(CMD_ERROR, &st, 1);
        }
    } else if (rx_len > expected) {
        rx_len = 0;
    }
}

void setup() {
    pinMode(LED_BUILTIN, OUTPUT);
    digitalWrite(LED_BUILTIN, LOW);
    Serial.begin(PC_SERIAL_BAUD);
}

void loop() {
    while (Serial.available() > 0) {
        feed_parser((uint8_t)Serial.read());
    }
}
