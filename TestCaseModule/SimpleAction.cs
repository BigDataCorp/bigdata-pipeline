﻿using BigDataPipeline.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCaseModule
{
    public class SimpleAction : IActionModule
    {
        public string GetDescription ()
        {
            return "";
        }

        public IEnumerable<ModuleParameterDetails> GetParameterDetails ()
        {
            yield return new ModuleParameterDetails ("Parameter1", typeof (string), "First parameter...", true);
        }

        public bool Execute (ISessionContext context)
        {
            var log = context.GetLogger ();

            var inputStream = context.GetInputStream ();
            log.Info (inputStream != null ? "Stream found!!!" : "Stream not found");

            Record inputData = inputStream != null ? inputStream.GetStream ().FirstOrDefault () : new Record ();
                      
            string kind = inputData.Get ("kind", "<empty>");
            log.Info ("kind = " + kind);
            log.Info ("count = " + inputData.Get ("count", "0"));

            // increment counter
            int c = inputData.Get ("count", 0);
            inputData.Set ("count", c + 1);

            // fire other events
            if (inputData.Get ("count", 0) < 2)
            {
                context.EmitEvent ("this.testCase1", new Record ().Set ("kind", "local").Set ("count", inputData.Get ("count", 0)));
                context.EmitEvent ("testCase2", new Record ().Set("kind", "global").Set ("count", inputData.Get ("count", 0)));
            } 
            

            return true;
        }
    }
}
