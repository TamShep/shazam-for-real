﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

class Program {
    const int SAMPLE_RATE = 16000;
    const int CHUNK_SIZE = 128;
    const int CHUNK_COUNT = 16;
    const int FFT_SIZE = CHUNK_SIZE * CHUNK_COUNT;
    const int RADIUS = 48;

    static void Main(string[] args) {
        Console.WriteLine("SPACE - tag, Q - quit");

        while(true) {
            var key = Console.ReadKey(true);

            if(Char.ToLower(key.KeyChar) == 'q')
                break;

            if(key.Key == ConsoleKey.Spacebar) {
                Console.Write("Listening... ");

                var result = CaptureAndTag();

                if(result.Success) {
                    Console.CursorLeft = 0;
                    Console.WriteLine(result.Url);
                    Process.Start("explorer", result.Url);
                } else {
                    Console.WriteLine(":(");
                }
            }
        }

    }

    static ShazamResult CaptureAndTag() {
        var wave = new WaveWindow(SAMPLE_RATE, CHUNK_SIZE, CHUNK_COUNT);
        var spectro = new Spectrogram(FFT_SIZE);
        var landmarks = new Landmarks(spectro, RADIUS, FreqToBin(Sig.FREQ_0), FreqToBin(Sig.FREQ_4));

        using(var capture = new WasapiCapture()) {
            var captureBuf = new BufferedWaveProvider(capture.WaveFormat) { ReadFully = false };

            capture.DataAvailable += (s, e) => {
                captureBuf.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            capture.StartRecording();

            using(var resampler = new MediaFoundationResampler(captureBuf, new WaveFormat(SAMPLE_RATE, 16, 1))) {
                var retryMs = 3000;
                var tagId = Guid.NewGuid().ToString();

                while(true) {
                    while(captureBuf.BufferedDuration.TotalSeconds < 1)
                        Thread.Sleep(100);

                    var chunkBuf = new byte[2 * CHUNK_SIZE];
                    if(resampler.Read(chunkBuf, 0, chunkBuf.Length) != chunkBuf.Length)
                        throw new Exception();

                    var chunk = new short[CHUNK_SIZE];
                    for(var i = 0; i < CHUNK_SIZE; i++)
                        chunk[i] = BitConverter.ToInt16(chunkBuf, i * 2);

                    wave.AddChunk(chunk);

                    if(wave.IsFull)
                        spectro.AddStripe(wave.GetSamples());

                    if(spectro.StripeCount > 2 * RADIUS)
                        landmarks.Detect(spectro.StripeCount - RADIUS - 1);

                    if(wave.ProcessedMs >= retryMs) {
                        //new Painter(spectro, landmarks).Paint("c:/temp/spectro.png");

                        var sigBytes = Sig.Write(wave.ProcessedSamples, CreateLandmarkInfos(spectro, landmarks));
                        var result = ShazamApi.SendRequest(tagId, wave.ProcessedMs, sigBytes).GetAwaiter().GetResult();
                        if(result.Success)
                            return result;

                        retryMs = result.RetryMs;
                        if(retryMs == 0)
                            return result;
                    }
                }
            }
        }
    }

    static IReadOnlyCollection<LandmarkInfo> CreateLandmarkInfos(Spectrogram spectro, Landmarks landmarks) {
        var locations = landmarks.Locations;
        var result = new List<LandmarkInfo>(locations.Count);

        foreach(var (stripe, bin) in locations) {
            result.Add(new LandmarkInfo(
                stripe,
                Convert.ToUInt16(64 * bin - 1),
                Convert.ToUInt16(UInt16.MaxValue * spectro.GetMagnitude(stripe, bin) / spectro.MaxMagnitude),
                BinToFreq(bin)
            ));
        }

        return result;
    }

    static int FreqToBin(double freq) {
        return Convert.ToInt32(freq * FFT_SIZE / SAMPLE_RATE);
    }

    static double BinToFreq(int bin) {
        return 1d * bin * SAMPLE_RATE / FFT_SIZE;
    }
}
