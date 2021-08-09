using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace FlagCollectorClient
{
    class Program
    {
        static void Main(string[] args)
        {
            ENetClient.Client c = new ENetClient.Client();
            c.OnCambioValClave += ArriveNewEvent;
            c.Conectar(System.Net.IPAddress.Parse("192.168.99.1"), 5000, "coordinator");
            while (!c.EstaConectado)
                Thread.Sleep(1000);
            if (c.EstaConectado)
            {
                c.Suscribirse("cave", "position");
                c.Suscribirse("cave", "answer");
                int i = 0;
                while (i<10)
                {
                    Thread.Sleep(5000);
                    i += 1;
                }
            }
            c.Desconectar();
            
        }

        public static void ArriveNewEvent(string client, string key, string newValue)
        {
            if (key.Equals("position"))
            {
                JObject json = JObject.Parse(newValue);
                ;
                Console.WriteLine("Pos: " + json.PropertyValues().First()+","+json.PropertyValues().Last());
            }
        }
    }
}
