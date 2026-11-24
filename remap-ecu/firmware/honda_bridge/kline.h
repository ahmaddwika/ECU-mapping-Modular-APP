#pragma once
#include <stdint.h>
#include <stddef.h>
#include <Arduino.h>

typedef struct {
    uint32_t baud;
    uint8_t rx_pin;
    uint8_t tx_pin;
} kline_config_t;

void kline_begin(const kline_config_t* cfg);
void kline_end(void);
bool kline_active(void);

int kline_transceive(const uint8_t* tx, size_t tx_len,
                     uint8_t* rx, size_t rx_max, uint32_t timeout_ms);

int kline_init_5baud(uint32_t target_baud);
int kline_init_fast(uint32_t target_baud);
