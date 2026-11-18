using System.Globalization;
namespace LabSorting.UI
{
    public static class UIFormat
    {
        // "4,042" — large counts read at a glance; invariant so every device shows the same separator.
        public static string Count(long value)=>value.ToString("N0",CultureInfo.InvariantCulture);
    }
    // Colour-codes a toast by what happened: green tick for something gained, orange "!" for a problem.
    public static class ToastTone
    {
        public enum Tone{Info,Success,Alert}
        static readonly string[] Alerts={"fail","cancel","unavailable","not enough","not available","error","offline","no internet","could not","couldn","is full","hands full","try again","declined","no charge"};
        static readonly string[] Successes={"added","delivered","received","restored","saved","unlocked","upgraded","purchased","thank","complete"};
        public static Tone Of(string text)
        {
            if(string.IsNullOrEmpty(text))return Tone.Info;var lower=text.ToLowerInvariant();
            foreach(var word in Alerts)if(lower.Contains(word))return Tone.Alert;
            foreach(var word in Successes)if(lower.Contains(word))return Tone.Success;
            return Tone.Info;
        }
    }
}
