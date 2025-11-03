import sys,os,json,time,threading
import mido
import fluidsynth

def println(obj):
    sys.stdout.write(json.dumps(obj,ensure_ascii=False)+"\n")
    sys.stdout.flush()

class SafeFlag:
    def __init__(self):
        self. lock = threading.Lock()
        self. val = False
    def set(self,v:bool):
        with self._lock:
            self._val = v
    def get(self) -> bool:
        with self._lock:
            return self._val

class MidiDemon:
    def __init__(self):
        self.fs = None
        self.sfid = None
        self.initailized = False
        self.play_thread = None
        self.playing_id = None
        self.stop_flag = SafeFlag()

    def init(self,sf2:str,samplerate:int = 44100,driver: str = "dsound",gain:float =0.5):
        if not os.oath.isfile(sf2):
            println({"error":"sf2 not found","path":sf2})
            return
        try:
            self.fs = fluidsynth.Synth(samplerate=samplerate, gain=gain)
            self.fs.start(driver =driver)
            self.sfid = self.fs.sfload(sf2)
            for ch in range(16):
                bank = 128 if ch == 9 else 0
                self.fs.program_select(ch,self.sfid,bank,0)
                self.initailized = True
                println({"readyt":True,"sf2_loded":True,"samplerate":samplerate, "driver": driver})
        except Exception as e:
            println({"error":"init_failed","detail":str(e)})

        def _play_mid_worker(self,mid_path:str,req_id:str):
            try:
                mid = mido.MidiFile(mid_path)
                tempo=mido.bpm2tempo(120)
                tpq =mid.ticks_per_beat

                for msg in mid:
                    if self.stop_flag.get() or self.playing_id != req_id:
                        break
                    delay = msg.time * mido.tempo2seconds(tempo,tpq)
                    if delay > 0:
                        remain = delay
                        step = 0.01
                        while remain > 0:
                            if self.stop_flag.get() or self.playing_id != req_id:
                                break
                            t = step if remain > step else remain
                            time.sleep(t)
                            remain -= t
                        if self.stop_flag.get() or self.playing_id != req_id:
                            break
                        if msg.type == 'set_tempo':
                            tempo = msg.tempo
                        elif msg.type == 'note_on':
                            self.fs.noteon(msg.channel, msg.note, msg.velocity)
                    elif msg.type == 'note_off':
                        self.fs.noteoff(msg.channel, msg.note)
                    elif msg.type == 'program_change':
                        bank = 128 if msg.channel == 9 else 0
                        self.fs.program_select(msg.channel, self.sfid, bank, msg.program)
                    elif msg.type == 'control_change':
                        self.fs.cc(msg.channel, msg.control, msg.value)
                    elif msg.type == 'pitchwheel':
                        self.fs.pitch_bend(msg.channel, msg.pitch)
                println({"stopped": req_id})
            except Exception as e:
                println({"error": "play_mid failed", "detail": str(e), "path": mid_path})
            finally:
                if self.playing_id == req_id:
                    self.playing_id = None
                self.stop_flag.set(False)

    def play_mid(self,req_id,str,mid_path:str):
        if not self.initailized:
            println({"error":"not initialized"});return
        if not os.path.isfile(mid_path):
            println({"error":"mid not found","path":mid_path}); return
        if self.play_thread and self.play_thread.is_alive():
            self.stop_flag.set(True)
            self.play_thread.join(timeout=1.0)
        self.stop_flag.set(False)
        self.playing_id = req_id
        self.play_thread = threading.Thread(target=self.play_mid_worker,args=(mid_path,req_id),daemon=True)
        self.play_thread.start()
        println({"playing":req_id,"path":mid_path})

    def note_off(self,ch:int,note:int):
        if not self.initialized:
            println({"error":"not initalized"});return
        self.fs.noteoff(ch,note)
        println({"event":"note_off","ch":ch,"note":note})
    def program(self, ch: int, program: int, drum: bool = False):
        if not self.initialized:
            println({"error":"not initialized"});return
        bank = 128 if (drum or ch == 9)else 0
        self.fs.program_select(ch,sfid.bank,program)
        println({"event":"program","ch":ch,"program":program,"bank": bank})

    def shutdown(self):
        try:
            if self.play_thread and self.play_thread.is_alive():
                self.stop_flag.set(True)
                self.play_thread.join(timeout=1.0)
        finally:
            if self.fs is not None:
                self.fs.delete()
            println({"bye": True})


    def main():
        daemon = MidiDaemon()
        println({"hello": True, "api": ["init", "play_mid", "note", "note_off", "program", "shutdown"]})

    for line in sys.stdin:
        if not line:
            break
        try:
            cmd = json.loads(line)
        except Exception as e:
            println({"error": "json", "detail": str(e)}); continue


        t = cmd.get("type")
        try:
            if t == "init":
                daemon.init(
                    sf2=cmd["sf2"],
                    samplerate=cmd.get("samplerate", 44100),
                    driver=cmd.get("driver", "dsound"),
                    gain=float(cmd.get("gain", 0.5)))
            elif t == "play_mid":
                daemon.play_mid(cmd.get("id", "bgm"), cmd["path"])
            elif t == "note":
                daemon.note(int(cmd.get("ch", 0)), int(cmd["note"]), int(cmd.get("vel", 100)))
            elif t == "note_off":
                daemon.note_off(int(cmd.get("ch", 0)), int(cmd["note"]))
            elif t == "program":
                daemon.program(int(cmd.get("ch", 0)), int(cmd.get("program", 0)), bool(cmd.get("drum", False)))
            elif t == "shutdown":
                daemon.shutdown(); break
            else:
                println({"error": "unknown cmd", "cmd": cmd})
        except KeyError as e:
            println({"error": "missing field", "detail": str(e), "cmd": cmd})
        except Exception as e:
                println({"error": "exception", "detail": str(e), "cmd": cmd})


if __name__ == "__main__":
    main()