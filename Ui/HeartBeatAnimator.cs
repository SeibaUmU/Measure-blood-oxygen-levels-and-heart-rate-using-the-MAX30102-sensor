using System;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MAX30102_WPF.Ui
{
    /// <summary>Làm trái tim đập theo nhịp tim đo được bằng cách co giãn một <see cref="ScaleTransform"/>.</summary>
    public sealed class HeartBeatAnimator
    {
        private readonly ScaleTransform scale;
        private int currentBpm;

        public HeartBeatAnimator(ScaleTransform scale)
        {
            this.scale = scale;
        }

        /// <summary>Đặt nhịp đập theo BPM. Ngoài khoảng 30..220 (hoặc 0) thì dừng, không nhấp nháy.</summary>
        public void SetRate(int bpm)
        {
            if (bpm < 30 || bpm > 220) bpm = 0;
            if (bpm == currentBpm) return;
            currentBpm = bpm;

            if (bpm == 0)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                return;
            }

            // Một nhịp = "tum-tum": phình mạnh, về, phình nhẹ, về, nghỉ
            var beat = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(60000.0 / bpm),
                RepeatBehavior = RepeatBehavior.Forever
            };
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.00)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.25, KeyTime.FromPercent(0.12)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.26)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromPercent(0.38)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.52)));
            beat.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(1.00)));

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, beat);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, beat);
        }
    }
}
