using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace calculate
{
    public class Calculate
    {
        public int Execute(string op, int a, int b)
        {
            int value = 0;
            switch (op)
            {
                case "+":
                    value = a + b;
                    break;
                case "-":
                    value = a - b;
                    break;
                case "*":
                    value = a * b;
                    break;
                case "/":
                    value = a / b;
                    break;
                default:
                    break;
            }
            return value;
        }
    }
}
