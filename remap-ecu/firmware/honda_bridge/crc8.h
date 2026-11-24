#pragma once
#include <stdint.h>
#include <stddef.h>

uint8_t crc8_update(uint8_t crc, uint8_t byte);
uint8_t crc8_bytes(const uint8_t* data, size_t len);
