using System;

namespace DesignTimeData.Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            Xamarin.Forms.DesignTimeData.IsEnabled = true;

            Console.WriteLine(Xamarin.Forms.DesignMode.IsDesignModeEnabled.ToString());
            Console.ReadLine();
        }
    }
}
