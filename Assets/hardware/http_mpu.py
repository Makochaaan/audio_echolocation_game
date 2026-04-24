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
# ssid =  "001D73222861_G"
# password = "44403393"

ssid =  "D821DA640EA1-2G"
password = "mf2263ncmrge96"
print(ssid)
# ===== LED（/light/on|off 用。不要なら下のルートごと削除OK） =====
try:
    led = Pin("LED", Pin.OUT)
except:
    led = Pin(25, Pin.OUT)

# ===== I2C & センサ =====
# 配線: GP0→SDA, GP1→SCL, 3V3→VCC, GND→GND, AD0→GND(=0x68)
i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
mpu = None  # Wi-Fi接続後に初期化

# ===== HTML =====
html = """<!DOCTYPE html>
<html>
  <head><meta charset="utf-8"><title>Pico W</title></head>
  <body>
    <h1>Pico W</h1>
    <p>%s</p>
    <p>MPU JSON: <code>/api/mpu</code></p>
  </body>
</html>
"""

wlan = network.WLAN(network.STA_IF)

def connect_to_network():
    wlan.active(True)
    try:
        wlan.config(pm=0xa11140)  # 省電力オフ（安定化）
    except:
        pass
    wlan.connect(ssid, password)

    max_wait = 10
    while max_wait > 0:
        st = wlan.status()
        if st < 0 or st >= 3:
            break
        max_wait -= 1
        print("waiting for connection...")
        time.sleep(1)

    if wlan.status() != 3:
        raise RuntimeError("network connection failed")
    print("connected, ip =", wlan.ifconfig()[0])

def read_sensor():
    ax, ay, az = mpu.accel.xyz  # g
    gx, gy, gz = mpu.gyro.xyz   # °/s
    return {
        "accel": {"x": ax, "y": ay, "z": az},
        "gyro":  {"x": gx, "y": gy, "z": gz}
    }

async def serve_client(reader, writer):
    try:
        req_line = await reader.readline()  # b"GET /path HTTP/1.1\r\n"
        # ヘッダ捨て
        while True:
            line = await reader.readline()
            if not line or line == b"\r\n":
                break

        try:
            first = req_line.decode()
        except:
            first = str(req_line)
        parts = first.split()
        path = parts[1] if len(parts) >= 2 else "/"
        print("Request:", first.strip())

        if path == "/api/mpu":
            payload = json.dumps(read_sensor()).encode()
            writer.write(b"HTTP/1.0 200 OK\r\n"
                         b"Content-Type: application/json\r\n"
                         b"Access-Control-Allow-Origin: *\r\n"
                         b"Connection: close\r\n\r\n")
            writer.write(payload)

        elif path == "/light/on":
            led.value(1)
            body = html % "LED is ON"
            writer.write(b"HTTP/1.0 200 OK\r\n"
                         b"Content-Type: text/html; charset=utf-8\r\n"
                         b"Access-Control-Allow-Origin: *\r\n"
                         b"Connection: close\r\n\r\n")
            writer.write(body.encode())

        elif path == "/light/off":
            led.value(0)
            body = html % "LED is OFF"
            writer.write(b"HTTP/1.0 200 OK\r\n"
                         b"Content-Type: text/html; charset=utf-8\r\n"
                         b"Access-Control-Allow-Origin: *\r\n"
                         b"Connection: close\r\n\r\n")
            writer.write(body.encode())

        else:
            body = html % "Hello from Pico W"
            writer.write(b"HTTP/1.0 200 OK\r\n"
                         b"Content-Type: text/html; charset=utf-8\r\n"
                         b"Access-Control-Allow-Origin: *\r\n"
                         b"Connection: close\r\n\r\n")
            writer.write(body.encode())

        await writer.drain()
    except Exception as e:
        msg = json.dumps({"error": str(e)}).encode()
        try:
            writer.write(b"HTTP/1.0 500 Internal Server Error\r\n"
                         b"Content-Type: application/json\r\n"
                         b"Access-Control-Allow-Origin: *\r\n"
                         b"Connection: close\r\n\r\n")
            writer.write(msg)
            await writer.drain()
        except:
            pass
    finally:
        await writer.wait_closed()

async def main():
    connect_to_network()
    # センサ初期化
    global mpu
    mpu = MPU6050(i2c)          # AD0=GNDで0x68
    mpu.accel_range = 1         # ±?g
    mpu.gyro_range  = 2         # ±1000°/s
    mpu.filter_range = 2        # 41Hz

    server = await asyncio.start_server(serve_client, "0.0.0.0", 80)
    print("Server on http://{}/".format(wlan.ifconfig()[0]))
    while True:
        await asyncio.sleep(3600)

try:
    asyncio.run(main())
finally:
    asyncio.new_event_loop()
