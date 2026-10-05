namespace GilbParser;

internal static class SampleCodes
{
    /// <summary>
    /// Аналог рис. 2–3 методички: 5 ветвей выбора = 4 оператора if, CLI = 3, 11 операторов.
    /// </summary>
    public const string TextbookExample =
        """
        fun calc(x: Double): Int {
            val y: Int
            if (x < 0) {
                y = 0
            } else if (x == 0.0) {
                y = 1
            } else if (x < 0.5) {
                y = 2
            } else if (x < 1) {
                y = 3
            } else {
                y = 4
            }
            return y
        }
        """;

    /// <summary>
    /// Анализируемая программа: for, while, do-while, if/else if, when.
    /// </summary>
    public const string LabProgram =
        """
        fun processControl(scores: Array<Int>, mode: Int): Int {
            var result = 0
            var i = 0

            while (i < scores.size) {
                var value = scores[i]

                if (value < 0) {
                    value = 0
                } else if (value > 100) {
                    value = 100
                }

                when (mode) {
                    1 -> result += value
                    2 -> {
                        if (value >= 50) {
                            result += 1
                        }
                    }
                    3 -> result *= value
                    else -> result -= value
                }

                i += 1
            }

            for (score in scores) {
                if (score == 0) {
                    result += 10
                }
            }

            var retries = 2
            do {
                if (result < 0) {
                    result = 0
                }
                retries -= 1
            } while (retries > 0)

            return result
        }
        """;
}
