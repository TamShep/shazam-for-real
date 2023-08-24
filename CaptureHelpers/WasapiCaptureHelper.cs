﻿using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

class WasapiCaptureHelper : ICaptureHelper {
    readonly WasapiCapture Capture;
    readonly BufferedWaveProvider CaptureBuf;
    readonly MediaFoundationResampler Resampler;

    public WasapiCaptureHelper() {
        Capture = CaptureSourceHelper.Loopback ? new WasapiLoopbackCapture() : new WasapiCapture();
        CaptureBuf = new BufferedWaveProvider(Capture.WaveFormat) { ReadFully = false };
        Resampler = new MediaFoundationResampler(CaptureBuf, ICaptureHelper.WAVE_FORMAT);
    }

    public void Dispose() {
        Resampler.Dispose();
        Capture.Dispose();
    }


    public bool Live => true;
    public ISampleProvider SampleProvider { get; private set; }

    public void Start() {
        Capture.DataAvailable += (s, e) => {
            CaptureBuf.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        Capture.StartRecording();

        SampleProvider = Resampler.ToSampleProvider();
    }
}
