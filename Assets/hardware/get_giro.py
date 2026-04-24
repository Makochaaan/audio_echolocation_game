# pico_mpu_http_async.py  — Heartbeatなし版
import network
import time
import uasyncio as asyncio
try:
    import ujson as json
except ImportError:
    import json

from machine import Pin, I2C
from imu import MPU6050  # Vector3d を返す版

# ===== Wi-Fi設定 =====
ssid =  "D821DA640EA1-2G"
password = "mf2263ncmrge96"

# ===== LED（/light/on|off 用。不要なら下のルートごと削除OK） =====
try:
    led = Pin("LED", Pin.OUT)
except:
    led = Pin(25, Pin.OUT)

# ===== I2C & センサ =====
# 配線: GP0→SDA, GP1→SCL, 3V3→VCC, GND→GND, AD0→GND(=0x68)
i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
mpu = MPU6050(i2c)  # Wi-Fi接続後に初期化

def read_sensor():
    ax, ay, az = mpu.accel.xyz  # g
    gx, gy, gz = mpu.gyro.xyz   # °/s
    return {
        "accel": {"x": ax, "y": ay, "z": az},
        "gyro":  {"x": gx, "y": gy, "z": gz}
    }

while True :
	print(read_sensor())
