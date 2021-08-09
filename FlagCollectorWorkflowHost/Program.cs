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
using ENetClient;
using Newtonsoft.Json.Linq;

namespace FlagCollectorWorkflowHost
{
    internal class Program
    {
        private static WorkflowApplication _wfApp;
        private static Client _c;

        private static void Main()
        {
            char separador = ':';
            // Lectura de parametros desde el archivo
            string[] lines = File.ReadAllLines(Directory.GetCurrentDirectory() + "\\input.txt");
            string ip = lines[0].Split(separador)[1]; //IP:127.0.0.1 //"192.168.31.111"
            string esTrial = lines[1].Split(separador)[1]  ;
            string idSujeto = (lines[2].Split(separador)[1]); //IdSujeto:1
            int radio = int.Parse(lines[3].Split(separador)[1]); //Radio:5
            int cantBanderas = int.Parse(lines[4].Split(separador)[1]); //Cantidad de banderas
            int separacion = int.Parse(lines[5].Split(separador)[1]); //Separacion
            string[] colorBandera = lines[6].Split(separador)[1].Split(','); //Colores
            string[] escenariosString = lines[7].Split(separador)[1].Split(','); //Escenarios
            int[] idEscenario = new int[escenariosString.Length];
            for (int i = 0; i < escenariosString.Length; i++)
                idEscenario[i] = int.Parse(escenariosString[i]);
            string[] nroProtocoloString = lines[8].Split(separador)[1].Split(','); //Nro Protocolo
            int[] nroProtocolo = new int[nroProtocoloString.Length];
            for (int i = 0; i < nroProtocoloString.Length; i++)
                nroProtocolo[i] = int.Parse(nroProtocoloString[i]);
            string[] angulosString = lines[9].Split(separador)[1].Split(','); //new[] { 90, 120 };//Angulos:90,50,120
            int[] angulos = new int[angulosString.Length];
            for (int i = 0; i < angulosString.Length; i++)
                angulos[i] = int.Parse(angulosString[i]);
            string[] testMemoriaString = (lines[10].Split(separador)[1]).Split(','); //Test de memoria
            int[] testMemoria = new int[testMemoriaString.Length];
            for (int i = 0; i < testMemoria.Length; i++)
                testMemoria[i] = int.Parse(testMemoriaString[i]);
            string[] lateralidad = lines[11].Split(separador)[1].Split(','); //Lateral a elegir
            string[] pares = lines[12].Split(separador)[1].Split(','); //Aparecen
            AcomodarBanderas(radio, cantBanderas, colorBandera[0], separacion); //creacion de las banderas
            int idB1 = int.Parse(lines[13].Split(separador)[1]); //Id Bandera 1
            int idB2 = int.Parse(lines[14].Split(separador)[1]); //Id Bandera 2
            //Diccionario de argumentos
            var inputsEjercicio = new Dictionary<string, object>
            {
                {"TrainingFlag",esTrial},
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
            /*var inputsPreTraining = new Dictionary<string, object>
            {
                {"Radio", radio},
              {"BanderasCircunferencia", _banderas},
                {"ColorBandera", colorBandera[0]},
                {"ColorSeleccion", "azul"},
                {"Mostrar180", _hideElements},
                 {"Protocolo", nroProtocolo},
                {"Separacion", separacion},
                {"Agregar180", _addElements},
                {"Escenario", "null"}
            };*/
          
            /**Archivo de salida*/
            string date = DateTime.Now.ToString(CultureInfo.CurrentCulture).Replace("/", "");
            date = "Log_" + date.Replace(" ", "_");
            date = date.Replace(":", "");
            string pathLog = Directory.GetCurrentDirectory() + "\\logs\\" + date + ".txt";
            string pathResultado = Directory.GetCurrentDirectory() + "\\logs\\Resultados_" + date + ".txt";
            string pathRecorrido = Directory.GetCurrentDirectory() + "\\logs\\Recorrido_" + date + ".txt";
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

            //if (args == null) throw new ArgumentNullException("args");

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
            //: new WorkflowApplication(new PreTraining.PreTraining(), inputsPreTraining);
            
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

            _wfApp.Completed = delegate(WorkflowApplicationCompletedEventArgs e)
            {
                List<string> reporte = (List<string>) e.Outputs["Reporte"];
                using (StreamWriter file = new StreamWriter(pathResultado))
                {
                    foreach (string line in lines)
                    {
                        file.WriteLine(line);
                    }
                    foreach (string line in reporte)
                    {
                        file.WriteLine(line);
                    }
                }
                List<string> recorrido = (List<string>) e.Outputs["Recorrido"];
                using (StreamWriter file1 = new StreamWriter(pathRecorrido))
                {
                    foreach (string line in recorrido)
                    {
                        file1.WriteLine(line);
                    }
                }
                syncEvent.Set();
                _c.Desconectar();
            };

            _wfApp.Aborted = delegate(WorkflowApplicationAbortedEventArgs e)
            {
                Console.WriteLine(e.Reason);
                syncEvent.Set();
                Console.ReadKey();
                _c.Desconectar();
            };

            _wfApp.OnUnhandledException = delegate(WorkflowApplicationUnhandledExceptionEventArgs e)
            {
                Console.WriteLine(e.UnhandledException.ToString());
                Console.ReadKey();
                return UnhandledExceptionAction.Terminate;
            };

            _wfApp.Idle = delegate { idleEvent.Set(); };

            _wfApp.Run();

            // Loop until the workflow completes.
            WaitHandle[] handles = {syncEvent, idleEvent};
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
                var x = radio*(float) (Math.Cos((Math.PI*angulo/180)));
                var y = radio*(float) (Math.Sin((Math.PI*angulo/180)));
                angulo = angulo + separacion;
                _banderas.Add(new PointF(x, y));
                _addElements = _addElements + " { \"id\":\"" + i + "\", \"color\":\"" + colorSeleccion +
                               "\", \"x\":\"" + x.ToString(CultureInfo.InvariantCulture).Replace(",", ".") +
                               "\", \"y\":\"" +
                               y.ToString(CultureInfo.InvariantCulture).Replace(",", ".") +
                               "\", \"visible\":\"false\" },";
                _hideElements = _hideElements + " { \"id\":\"" + i + "\", \"visible\":\"true\" },";
            }

            _addElements = _addElements.Substring(0, _addElements.Length - 1);
            _addElements = _addElements + " ]";
            _hideElements = _hideElements.Substring(0, _hideElements.Length - 1);
            _hideElements = _hideElements + " ]";
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
                if (json == null) throw new ArgumentNullException("client");
                _wfApp.ResumeBookmark("RtaCave", json.PropertyValues().First());
            }
        }

        /**Tracking de los mensajes hacia la cave*/
        public class StatusTrackingParticipant : TrackingParticipant
        {
            protected override void Track(TrackingRecord record, TimeSpan timeout)
            {
                ActivityStateRecord asr = record as ActivityStateRecord;
               
                if (asr != null)
                {
                    if (asr.State == ActivityStates.Executing &&
                        asr.Activity.TypeName == "System.Activities.Statements.WriteLine")
                    {
                        string clave = asr.Arguments["Text"].ToString();
                        if (clave.StartsWith("L"))
                        {
                            _c.CambiarValorClave("load_visualkeys", clave.Substring(1));
                        }
                        if (clave.StartsWith("M"))
                        {
                            _c.CambiarValorClave("cross_position", clave.Substring(1));
                        }
                        if (clave.StartsWith("X"))
                        {
                            _c.CambiarValorClave("cross_color", clave.Substring(1));
                        }
                        if (clave.StartsWith("AS"))
                        {
                            _c.CambiarValorClave("add_elements", clave.Substring(2));
                        }
                        if (clave.StartsWith("A{"))
                        {
                            _c.CambiarValorClave("add_element", clave.Substring(1));
                        }
                        if (clave.StartsWith("HS"))
                        {
                            _c.CambiarValorClave("hide_elements", clave.Substring(2));
                        }
                        if (clave.StartsWith("hide_question"))
                        {
                            _c.CambiarValorClave("hide_question", "");
                        }
                        if (clave.StartsWith("hide_all"))
                        {
                            _c.CambiarValorClave("hide_all", "");
                        }
                        if (clave.StartsWith("Q{"))
                        {
                            _c.CambiarValorClave("show_question", clave.Substring(1));
                        }
                        if (clave.StartsWith("C{"))
                        {
                            _c.CambiarValorClave("change_color", clave.Substring(1));
                        }
                        if (clave.StartsWith("H{"))
                        {
                            _c.CambiarValorClave("hide_element", clave.Substring(1));
                        }
                        if (clave.StartsWith("O{"))
                        {
                            _c.CambiarValorClave("show_circle", clave.Substring(1));
                        }
                        if (clave.StartsWith("N{"))
                        {
                            _c.CambiarValorClave("hide_circle", clave.Substring(1));
                        }    
                    }
                }
            }
        }
    }
}