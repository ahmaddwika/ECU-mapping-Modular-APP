#pragma once
#include <stdint.h>
#include <stddef.h>
#include "crc8.h"

#define FRAME_STX 0xAA
#define FRAME_MAX_PAYLOAD 128

typedef enum {
    CMD_PING = 0x01,
    CMD_LOOPBACK = 0x02,
    CMD_KLINE_INIT = 0x10,
    CMD_KLINE_SEND = 0x11,
    CMD_ECU_ID = 0x12,
    CMD_SET_MODE = 0x20,
    CMD_READ_FLASH = 0x21,
    CMD_WRITE_FLASH = 0x22,
    CMD_CHECKSUM = 0x23,
    CMD_POWER_CYCLE = 0x30,
    CMD_MODULE_INFO = 0x40,
    CMD_ERROR = 0xFF
} cmd_t;

typedef enum {
    ST_OK = 0x00,
    ST_BAD_FRAME = 0x01,
    ST_UNKNOWN = 0x02,
    ST_NO_ECU = 0x03,
    ST_TIMEOUT = 0x04,
    ST_NOT_IMPL = 0x05,
    ST_BUSY = 0x06
} status_t;

static inline size_t frame_build(uint8_t* out, cmd_t cmd, const uint8_t* payload, size_t payload_len) {
    out[0] = FRAME_STX;
    out[1] = (uint8_t)payload_len;
    out[2] = (uint8_t)cmd;
    for (size_t i = 0; i < payload_len; i++) {
        out[3 + i] = payload[i];
    }
    out[3 + payload_len] = crc8_bytes(&out[1], payload_len + 2);
    return 3 + payload_len + 1;
}
