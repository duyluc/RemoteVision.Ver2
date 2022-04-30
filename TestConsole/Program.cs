using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                int c = 1;
                int a = 0;
                int b = c/a;
            }
            catch(DivideByZeroException t)
            {
                Console.WriteLine("on exception");
                return;
            }
            finally
            {
                Console.WriteLine("On finally");
                Console.ReadKey();
            }
            Console.WriteLine("Out");
            Console.ReadKey();
        }
    }
}
