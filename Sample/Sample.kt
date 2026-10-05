fun processControl(scores: Array<Int>, mode: Int): Int {
    var result = 0
    var i = 0

    // while — цикл с предусловием
    while (i < scores.size) {
        var value = scores[i]

        // if / else if / else — ветвление
        if (value < 0) {
            value = 0
        } else if (value > 100) {
            value = 100
        }

        // when — оператор множественного выбора (n = 4 ветви)
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

    // for — цикл по коллекции
    for (score in scores) {
        if (score == 0) {
            result += 10
        }
    }

    // do-while — цикл с постусловием
    var retries = 2
    do {
        if (result < 0) {
            result = 0
        }
        retries -= 1
    } while (retries > 0)

    return result
}
