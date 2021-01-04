using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SharpDX.Multimedia;
using SharpDX.XAudio2;
using System.IO;

namespace ChaosHelper
{
    public static class SharpDxSoundPlayer
    {
        /// <summary>
        /// Play a sound file. Supported format are Wav(pcm+adpcm) and XWMA
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="fileName">Name of the file.</param>
        public static void PlaySoundFile(string fileName, float volume)
        {
            var device = new XAudio2();
            var masteringVoice = new MasteringVoice(device);

            var stream = new SoundStream(File.OpenRead(fileName));
            var waveFormat = stream.Format;
            var buffer = new AudioBuffer
            {
                Stream = stream.ToDataStream(),
                AudioBytes = (int)stream.Length,
                Flags = BufferFlags.EndOfStream
            };
            stream.Close();

            var sourceVoice = new SourceVoice(device, waveFormat, true);
            sourceVoice.SetVolume(volume);

            sourceVoice.SubmitSourceBuffer(buffer, stream.DecodedPacketsInfo);
            sourceVoice.Start();

            while (sourceVoice.State.BuffersQueued > 0)
            {
                Thread.Sleep(10);
            }

            sourceVoice.DestroyVoice();
            sourceVoice.Dispose();
            buffer.Stream.Dispose();

            masteringVoice.Dispose();
            device.Dispose();
        }
    }
}
