#include "kline.h"

static HardwareSerial* kline_uart = nullptr;
static uint8_t kline_rx_pin = 0;
static uint8_t kline_tx_pin = 0;
static uint32_t kline_baud = 0;

void kline_begin(const kline_config_t* cfg) {
    kline_rx_pin = cfg->rx_pin;
    kline_tx_pin = cfg->tx_pin;
    kline_baud = cfg->baud;
    if (kline_uart != nullptr) {
        kline_uart->end();
    }
    kline_uart = &Serial1;
    kline_uart->begin(kline_baud, SERIAL_8N1, kline_rx_pin, kline_tx_pin);
}

void kline_end(void) {
    if (kline_uart != nullptr) {
        kline_uart->end();
        kline_uart = nullptr;
    }
}

bool kline_active(void) {
    return kline_uart != nullptr;
}

int kline_transceive(const uint8_t* tx, size_t tx_len,
                     uint8_t* rx, size_t rx_max, uint32_t timeout_ms) {
    if (kline_uart == nullptr) {
        return -1;
    }
    kline_uart->write(tx, tx_len);

    uint32_t start = millis();
    size_t got = 0;
    uint32_t last_byte = 0;

    while ((millis() - start) < timeout_ms) {
        while (kline_uart->available() > 0) {
            if (got < rx_max) {
                rx[got++] = (uint8_t)kline_uart->read();
            } else {
                (void)kline_uart->read();
            }
            last_byte = millis();
        }
        if (got > 0 && (millis() - last_byte) > 20) {
            break;
        }
    }
    return (int)got;
}

int kline_init_5baud(uint32_t target_baud) {
    (void)target_baud;
    return -1;
}

int kline_init_fast(uint32_t target_baud) {
    (void)target_baud;
    return -1;
}
