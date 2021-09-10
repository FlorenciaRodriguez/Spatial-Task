using ENetClient;
using Newtonsoft.Json.Linq;
using System;
using System.Activities;
using System.Activities.Tracking;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace FlagCollectorWorkflowHost
{
    class Program1
    {

        private static WorkflowApplication _wfApp;
        private static Client _c;

        public static void Main()
        {
            char separador = ':';
            string[] lines = File.ReadAllLines(Directory.GetCurrentDirectory() + "\\input.txt");
            Console.WriteLine("Lineas: "+lines.Length);
            string ip = (lines[0].Split(separador)[1]);
            //"127.0.0.1";
            string preotr = (lines[1].Split(separador)[1]);
            string idSujeto = (lines[2].Split(separador)[1]);
            int radio = int.Parse(lines[3].Split(separador)[1]);
            int cantBanderas = int.Parse(lines[4].Split(separador)[1]);
            int separacion = int.Parse(lines[5].Split(separador)[1]);
            string angulo = (lines[8].Split(separador)[1]);
            string color = lines[6].Split(separador)[1];
            string colorLinea = color;
            for (int i = 0; i < 15; i++)
                colorLinea += "," + color;
            string[] colorBandera = colorLinea.Split(',');
            string escena = lines[7].Split(separador)[1];
            string escenaLinea = escena;
            for (int i = 0; i < 15; i++)
                escenaLinea += "," + escena;
            string[] escenariosString = escenaLinea.Split(',');
            int[] idEscenario = new int[escenariosString.Length];
            for (int i = 0; i < escenariosString.Length; i++)
                idEscenario[i] = int.Parse(escenariosString[i]);
            string protocol = lines[10].Split(separador)[1];
            
            for (int i = 0; i < 20; i++)
            {
                protocol = protocol + "," + protocol;
            }
            string protocolos = "Nro Protocolo:" + protocol;
            string[] nroProtocoloString = protocolos.Split(separador)[1].Split(',');
            int[] nroProtocolo = new int[nroProtocoloString.Length];
            for (int i = 0; i < nroProtocoloString.Length; i++)
                nroProtocolo[i] = int.Parse(nroProtocoloString[i]);
            string angulos_ = "Angulos: "+angulo+", "+angulo;
            string[] angulosString = angulos_.Split(separador)[1].Split(',');
            int[] angulos = new int[angulosString.Length];
            for (int i = 0; i < angulosString.Length; i++)
                angulos[i] = int.Parse(angulosString[i]);
           string delay = lines[9].Split(separador)[1];
            for (int i = 0; i < 20; i++)
            {
                delay = delay + "," + delay;
            }
            string mem = "Test Memoria(1Si / 2No / 3Solo Evaluar):"+delay;
            string[] testMemoriaString = (mem.Split(separador)[1]).Split(',');
            int[] testMemoria = new int[testMemoriaString.Length];
            for (int i = 0; i < testMemoria.Length; i++)
                testMemoria[i] = int.Parse(testMemoriaString[i]);
            string lat = "Lateralidad(I / D / M):D,M,M,M,M,M,M,M,M,M,M,M,M,M,M,M,M";
            string[] lateralidad = lat.Split(separador)[1].Split(',');
            string par = "Pares(S / N):N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N,N";
            string[] pares = par.Split(separador)[1].Split(',');
            AcomodarBanderas(radio, cantBanderas, colorBandera[0], separacion);
            int idB1 = -1;
            int idB2 = -1;
            //Diccionario de argumentos
            var inputsEjercicio = new Dictionary<string, object>
            {
                {"TrainingFlag", preotr},
                {"Angulo", angulos},
                {"Lateral", lateralidad},
                {"Color", colorBandera},
                {"Protocolo", nroProtocolo},
                {"AparecePar", pares},
                {"Radio", radio},
                {"Escenario", idEscenario},
                {"Sujeto", idSujeto},
                {"Agregar180", _addElements},
                {"BanderasCircunferencia", _banderas},
                {"Separacion", separacion},
                {"Evaluar", testMemoria},
                {"Mostrar180", _hideElements},
                { "Bandera1",idB1},
                { "Bandera2",idB2}
            };

            //Diccionario PreTraining

            /**Archivo de salida*/
            string date = DateTime.Now.ToString(CultureInfo.CurrentCulture).Replace("/", "");
            date = "Log_" + date.Replace(" ", "_");
            date = date.Replace(":", "");
            string pathLog = Directory.GetCurrentDirectory() + "\\logs\\" + date + ".txt";
            string pathResultado = Directory.GetCurrentDirectory() + "\\Result_" + date + ".txt";
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\logs");
            using (File.Create(pathLog))
            {
            }

            //Archivo para guardar la salida de consola
            try
            {
                FileStream ostrm = new FileStream(pathLog, FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter writer = new StreamWriter(ostrm);
                Console.SetOut(writer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot open Redirect.txt for writing");
                Console.WriteLine(e.Message);
                return;
            }


            /**Conexion*/
            _c = new Client();

            _c.OnCambioValClave += ArriveNewEvent;
            _c.Conectar(IPAddress.Parse(ip), 5000, "coordinator");
            while (!_c.EstaConectado)
            {
                Thread.Sleep(1000);
            }

            if (_c.EstaConectado)
            {
                _c.Suscribirse("cave", "position");
                _c.Suscribirse("cave", "answer");
            }
            AutoResetEvent syncEvent = new AutoResetEvent(false);
            AutoResetEvent idleEvent = new AutoResetEvent(false);

            //Creacion del WF
            /**Workflow*/
            _wfApp = new WorkflowApplication(new Ejercicio.Ejercicio(), inputsEjercicio);

            //Tracking de los mensajes a la CAVE
            StatusTrackingParticipant stp = new StatusTrackingParticipant
            {
                TrackingProfile = new TrackingProfile
                {
                    Queries =
                    {
                        new ActivityStateQuery
                        {
                            ActivityName = "WriteLine",
                            States = {ActivityStates.Executing},
                            Arguments = {"Text"}
                        }
                    }
                }
            };
            _wfApp.Extensions.Add(stp);

            _wfApp.Completed = delegate (WorkflowApplicationCompletedEventArgs e)
            {
                List<string> reporte = (List<string>)e.Outputs["Reporte"];
                using (StreamWriter file = new StreamWriter(pathResultado))
                {
                    foreach (string line in reporte)
                    {
                        String[] nuevalinea = line.Split('\n');
                        foreach (string s in nuevalinea)
                        {
                            string n = s;
                            n = n.Replace(";", ": ");
                            n = n.Replace("Sujeto", "Participant Name");
                            n = n.Replace("Hora Inicio", "Trial strat time");
                            n = n.Replace("NroIteracion", "NoTrial");
                            n = n.Replace("IdBandera:", "Flag Taget [id]:");
                            n = n.Replace("IdBanderaX:", "Flag Taget [X]:");
                            n = n.Replace("IdBanderaY:", "Flag Taget [Y]:");
                            n = n.Replace("Hora:", "Flag catch time:");
                            n = n.Replace("Inicio test memoria", "Start test time");
                            n = n.Replace("Hora Fin Seleccion", "Finish test time");
                            n = n.Replace("Bandera Seleccionada", "Selected flag");
                            n = n.Replace("[id]", "(id)");
                            if (!n.Contains("Protocolo") && !n.Contains("Radio") && !n.Contains("Correcta") && !n.Contains("Captura Bandera") && !n.Equals("\n"))
                            {
                                file.WriteLine(n);
                            }
                        }
                    }
                }
                syncEvent.Set();
                _c.Desconectar();
            };

            _wfApp.Aborted = delegate (WorkflowApplicationAbortedEventArgs e)
            {
                Console.WriteLine(e.Reason);
                syncEvent.Set();
                Console.ReadKey();
                _c.Desconectar();
            };

            _wfApp.OnUnhandledException = delegate (WorkflowApplicationUnhandledExceptionEventArgs e)
            {
                Console.WriteLine(e.UnhandledException.ToString());
                Console.ReadKey();
                return UnhandledExceptionAction.Terminate;
            };

            _wfApp.Idle = delegate { idleEvent.Set(); };

            _wfApp.Run();

            // Loop until the workflow completes.
            WaitHandle[] handles = { syncEvent, idleEvent };
            while (WaitHandle.WaitAny(handles) != 0)
            {
            }
            _c.Desconectar();

        }

        private static string _addElements;
        private static string _hideElements;
        private static List<PointF> _banderas;

        private static void AcomodarBanderas(int radio, int cantBanderas, string colorSeleccion, int separacion)
        {
            _banderas = new List<PointF>();

            int angulo = 0;
            _addElements = "AS[";
            _hideElements = "HS[";
            for (int i = 0; i < cantBanderas; i++)
            {
                float x = radio * (float)Math.Cos(Math.PI * angulo / 180);
                float y = radio * (float)Math.Sin(Math.PI * angulo / 180);
                angulo += separacion;
                _banderas.Add(new PointF(x, y));
                _addElements = _addElements + " { \"id\":\"" + i + "\", \"color\":\"" + colorSeleccion +
                               "\", \"x\":\"" + x.ToString(CultureInfo.InvariantCulture).Replace(",", ".") +
                               "\", \"y\":\"" +
                               y.ToString(CultureInfo.InvariantCulture).Replace(",", ".") +
                               "\", \"visible\":\"false\" },";
                _hideElements = _hideElements + " { \"id\":\"" + i + "\", \"visible\":\"true\" },";
            }

            _addElements = _addElements.Substring(0, _addElements.Length - 1);
            _addElements += " ]";
            _hideElements = _hideElements.Substring(0, _hideElements.Length - 1);
            _hideElements += " ]";
        }

        /**Mansajes que llegan de la cave*/
        private static void ArriveNewEvent(string client, string key, string newValue)
        {
            if (client == null) throw new ArgumentNullException("client");
            if (key.Equals("position"))
            {
                JObject json = JObject.Parse(newValue);
                if (json != null)
                    _wfApp.ResumeBookmark("RtaCave",
                        json.PropertyValues().First() + ";" + json.PropertyValues().Last());
            }

            if (key.Equals("answer"))
            {
                JObject json = JObject.Parse(newValue);
                if (json == null)
                {
                    throw new ArgumentNullException("client");
                }

                _wfApp.ResumeBookmark("RtaCave", json.PropertyValues().First());
            }
        }

        /**Tracking de los mensajes hacia la cave*/
        public class StatusTrackingParticipant : TrackingParticipant
        {
            protected override void Track(TrackingRecord record, TimeSpan timeout)
            {
                if (record is ActivityStateRecord asr)
                {
                    if (asr.State == ActivityStates.Executing &&
                        asr.Activity.TypeName == "System.Activities.Statements.WriteLine")
                    {
                        string clave = asr.Arguments["Text"].ToString();
                        if (clave.StartsWith("L"))
                        {
                            _ = _c.CambiarValorClave("load_visualkeys", clave.Substring(1));
                        }
                        if (clave.StartsWith("M"))
                        {
                            _ = _c.CambiarValorClave("cross_position", clave.Substring(1));
                        }
                        if (clave.StartsWith("X"))
                        {
                            _ = _c.CambiarValorClave("cross_color", clave.Substring(1));
                        }
                        if (clave.StartsWith("AS"))
                        {
                            _ = _c.CambiarValorClave("add_elements", clave.Substring(2));
                        }
                        if (clave.StartsWith("A{"))
                        {
                            _ = _c.CambiarValorClave("add_element", clave.Substring(1));
                            Console.WriteLine("Se agrego: " + clave.Substring(1));
                        }
                        if (clave.StartsWith("HS"))
                        {
                            _ = _c.CambiarValorClave("hide_elements", clave.Substring(2));
                        }
                        if (clave.StartsWith("hide_question"))
                        {
                            _ = _c.CambiarValorClave("hide_question", "");
                        }
                        if (clave.StartsWith("hide_all"))
                        {
                            _ = _c.CambiarValorClave("hide_all", "");
                        }
                        if (clave.StartsWith("Q{"))
                        {
                            _ = _c.CambiarValorClave("show_question", clave.Substring(1));
                        }
                        if (clave.StartsWith("C{"))
                        {
                            _ = _c.CambiarValorClave("change_color", clave.Substring(1));
                        }
                        if (clave.StartsWith("H{"))
                        {
                            _ = _c.CambiarValorClave("hide_element", clave.Substring(1));
                        }
                        if (clave.StartsWith("O{"))
                        {
                            _ = _c.CambiarValorClave("show_circle", clave.Substring(1));
                        }
                        if (clave.StartsWith("N{"))
                        {
                            _ = _c.CambiarValorClave("hide_circle", clave.Substring(1));
                        }
                    }
                }
            }
        }
    }
}