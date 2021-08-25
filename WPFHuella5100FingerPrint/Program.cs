using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFHuella5100FingerPrint
{
    class Program
    {
        private static ReadFingerPrint readFingerPrint;

        private static int countError = 0;

        static void Main(string[] args)
        {
            Activate();
        }

        private static void Activate()
        {
            try
            {
                readFingerPrint = new ReadFingerPrint(123456789);

                readFingerPrint.isMatch = response =>
                {
                    redirectView(response);
                };

                readFingerPrint.isCapture = capture =>
                {
                    loadView(capture);
                };

                readFingerPrint.star();
                Console.ReadKey();
            }
            catch (Exception ex)
            {
            }
        }

        private static void loadView(bool capture)
        {
            try
            {
                if (capture)
                {
                    Console.WriteLine("Lectura exitosa, Validando identidad");
                }
                else
                {
                    Console.WriteLine("Coloca tu huella en el lector");
                }
            }
            catch (Exception ex)
            {

            }
        }

        private static void redirectView(bool match)
        {
            if (match)
            {
                Console.WriteLine("Proceso Exitoso");
                readFingerPrint.stop();
            }
            else
            {
                if (countError < 3)
                {
                    Console.WriteLine("No se pudo reconocer la identidad, por favor vuelvelo a intentar");
                    countError++;
                    loadView(false);
                    readFingerPrint.star();
                }
                else
                {
                    Console.WriteLine("Max número de intentos");
                }
            }
        }
    }
}
