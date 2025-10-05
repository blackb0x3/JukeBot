using System.Threading.Channels;
using JukeBot.Core.Audio;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Services;

public class AudioAnalysisService : IDisposable
{
    private readonly ILogger<AudioAnalysisService> _logger;
    private readonly Channel<AudioDataEventArgs> _audioDataChannel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;
    private readonly Task _simulationTask;
    private float[] _spectrumData;
    private readonly object _spectrumLock = new();
    private readonly int _fftSize = 2048;
    private readonly int _spectrumBins;
    private IAudioBackend? _backend;
    private readonly Random _random = new();

    public event EventHandler<SpectrumDataEventArgs>? SpectrumUpdated;

    public AudioAnalysisService(int spectrumBins, ILogger<AudioAnalysisService> logger)
    {
        _spectrumBins = spectrumBins;
        _logger = logger;
        _spectrumData = new float[spectrumBins];
        _audioDataChannel = Channel.CreateBounded<AudioDataEventArgs>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessAudioDataAsync(_cts.Token));
        _simulationTask = Task.Run(() => SimulateSpectrumAsync(_cts.Token));
    }

    public void AttachToBackend(IAudioBackend backend)
    {
        _backend = backend;
        backend.AudioDataAvailable += OnAudioDataAvailable;
        _logger.LogInformation("Attached to audio backend for analysis");
    }

    public void DetachFromBackend(IAudioBackend backend)
    {
        backend.AudioDataAvailable -= OnAudioDataAvailable;
        _logger.LogInformation("Detached from audio backend");
    }

    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        // Try to write to channel, but don't block if full
        _audioDataChannel.Writer.TryWrite(e);
    }

    private async Task ProcessAudioDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Audio analysis processing started");

        await foreach (var audioData in _audioDataChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var spectrum = ComputeSpectrum(audioData.Samples, audioData.SampleRate);

                lock (_spectrumLock)
                {
                    _spectrumData = spectrum;
                }

                SpectrumUpdated?.Invoke(this, new SpectrumDataEventArgs
                {
                    Spectrum = spectrum
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio data");
            }
        }

        _logger.LogInformation("Audio analysis processing stopped");
    }

    private float[] ComputeSpectrum(float[] samples, int sampleRate)
    {
        // Take only the samples we need for FFT
        var fftInput = new System.Numerics.Complex[_fftSize];
        var samplesNeeded = Math.Min(samples.Length, _fftSize);

        // Convert to mono if needed and apply Hann window
        var window = Window.Hann(_fftSize);
        for (int i = 0; i < samplesNeeded; i++)
        {
            var windowValue = window[i];
            fftInput[i] = new System.Numerics.Complex(samples[i] * windowValue, 0);
        }

        // Pad with zeros if needed
        for (int i = samplesNeeded; i < _fftSize; i++)
        {
            fftInput[i] = System.Numerics.Complex.Zero;
        }

        // Perform FFT
        Fourier.Forward(fftInput, FourierOptions.AsymmetricScaling);

        // Convert to magnitudes and bin logarithmically
        return BinSpectrum(fftInput, sampleRate);
    }

    private float[] BinSpectrum(System.Numerics.Complex[] fftOutput, int sampleRate)
    {
        var bins = new float[_spectrumBins];
        var usableBins = _fftSize / 2; // Only use first half (Nyquist)

        // Logarithmic binning for better frequency distribution
        var minFreq = 20.0; // Hz
        var maxFreq = Math.Min(20000.0, sampleRate / 2.0); // Hz
        var logMin = Math.Log10(minFreq);
        var logMax = Math.Log10(maxFreq);

        for (int i = 0; i < _spectrumBins; i++)
        {
            // Calculate frequency range for this bin
            var logFreqStart = logMin + (logMax - logMin) * i / _spectrumBins;
            var logFreqEnd = logMin + (logMax - logMin) * (i + 1) / _spectrumBins;
            var freqStart = Math.Pow(10, logFreqStart);
            var freqEnd = Math.Pow(10, logFreqEnd);

            // Convert frequency to FFT bin indices
            var binStart = (int)(freqStart * _fftSize / sampleRate);
            var binEnd = (int)(freqEnd * _fftSize / sampleRate);

            binStart = Math.Clamp(binStart, 0, usableBins - 1);
            binEnd = Math.Clamp(binEnd, binStart + 1, usableBins);

            // Average magnitude across this frequency range
            float sum = 0;
            int count = 0;
            for (int j = binStart; j < binEnd; j++)
            {
                sum += (float)fftOutput[j].Magnitude;
                count++;
            }

            if (count > 0)
            {
                bins[i] = sum / count;
            }
        }

        // Normalize and apply smoothing
        var maxMagnitude = bins.Max();
        if (maxMagnitude > 0)
        {
            for (int i = 0; i < bins.Length; i++)
            {
                bins[i] = bins[i] / maxMagnitude;

                // Apply logarithmic scaling for better visual representation
                bins[i] = (float)(Math.Log10(1 + bins[i] * 9) / Math.Log10(10));

                // Clamp to 0-1 range
                bins[i] = Math.Clamp(bins[i], 0f, 1f);
            }
        }

        return bins;
    }

    public float[] GetCurrentSpectrum()
    {
        lock (_spectrumLock)
        {
            return (float[])_spectrumData.Clone();
        }
    }

    private async Task SimulateSpectrumAsync(CancellationToken cancellationToken)
    {
        // Simulate spectrum data when audio callbacks aren't available
        _logger.LogInformation("Starting spectrum simulation (audio callbacks not available)");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(50, cancellationToken); // ~20 FPS

                if (_backend?.IsPlaying != true)
                {
                    // Reset spectrum when not playing
                    lock (_spectrumLock)
                    {
                        _spectrumData = new float[_spectrumBins];
                    }
                    continue;
                }

                // Generate simulated spectrum based on random music-like patterns
                var spectrum = new float[_spectrumBins];
                var time = DateTime.Now.Ticks / 10000000.0;

                for (int i = 0; i < _spectrumBins; i++)
                {
                    // Bass frequencies (lower bins) have more energy
                    var bassBoost = Math.Max(0, 1.0 - (i / (double)_spectrumBins));

                    // Create some variation with sine waves at different frequencies
                    var wave1 = Math.Sin(time * 2 + i * 0.1) * 0.3;
                    var wave2 = Math.Sin(time * 3 + i * 0.15) * 0.2;
                    var wave3 = Math.Sin(time * 5 + i * 0.05) * 0.15;
                    var noise = (_random.NextDouble() - 0.5) * 0.1;

                    var value = (wave1 + wave2 + wave3 + noise + 0.3) * bassBoost;
                    spectrum[i] = (float)Math.Clamp(value, 0, 1);
                }

                lock (_spectrumLock)
                {
                    _spectrumData = spectrum;
                }

                SpectrumUpdated?.Invoke(this, new SpectrumDataEventArgs { Spectrum = spectrum });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in spectrum simulation");
            }
        }

        _logger.LogInformation("Spectrum simulation stopped");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _audioDataChannel.Writer.Complete();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(2));
            _simulationTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing audio analysis service");
        }

        _cts.Dispose();
    }
}

public class SpectrumDataEventArgs : EventArgs
{
    public required float[] Spectrum { get; init; }
}
