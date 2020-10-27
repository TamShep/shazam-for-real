﻿using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Numerics;

class Spectrogram {
    readonly int FFTSize;
    readonly double[] Hann;
    readonly List<double[]> Stripes = new List<double[]>();

    public readonly int BinCount;

    public Spectrogram(int fftSize) {
        FFTSize = fftSize;
        Hann = Window.Hann(fftSize);
        BinCount = fftSize / 2 + 1;
    }

    public int StripeCount => Stripes.Count;
    public double MaxMagnitude { get; private set; }

    public void AddStripe(IReadOnlyList<short> samples) {
        if(samples.Count != FFTSize)
            throw new ArgumentException();

        var fft = new Complex[FFTSize];
        for(var i = 0; i < FFTSize; i++)
            fft[i] = new Complex(samples[i] * Hann[i], 0);

        Fourier.Forward(fft);

        var stripe = new double[BinCount];

        for(var bin = 0; bin < BinCount; bin++) {
            var magnitude = fft[bin].Magnitude;
            MaxMagnitude = Math.Max(MaxMagnitude, magnitude);

            stripe[bin] = magnitude;
        }

        Stripes.Add(stripe);
    }

    public double GetMagnitude(int stripe, int bin) {
        return Stripes[stripe][bin];
    }

}
