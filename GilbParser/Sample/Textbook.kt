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
