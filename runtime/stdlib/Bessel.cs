namespace GoCLR.Stdlib;

using SM = System.Math;

// Bessel functions of the first and second kinds — a direct port of Go's
// math/j0.go, math/j1.go and math/jn.go (themselves simplified ports of
// FreeBSD's fdlibm e_j0.c / e_j1.c / e_jn.c). Kept byte-identical to Go: the
// polynomial branches (|x| < 2) are pure float64 arithmetic and so are exact;
// the |x| >= 2 branches call Sin/Cos/Sqrt, which goclr maps to System.Math
// exactly as it maps math.Sin/Cos/Sqrt (so they are as exact as goclr's own
// trig — see the last-ULP note in LIMITATIONS for very large arguments).
public static partial class Math
{
    // Go evaluates 1/SqrtPi and 2/Pi as high-precision untyped constants and then
    // rounds to float64 — NOT as a float64 division of the rounded SqrtPi/Pi. Use the
    // correctly-rounded literals so the asymptotic branches match Go bit-for-bit.
    private const double OneOverSqrtPi = 0.5641895835477562869480794515607725858440506293289988568440857217106424684414934144794; // 1/sqrt(pi)
    private const double TwoOverPi = 0.63661977236758134307553505349005744813783858296182579499066937623558364471560794982097; // 2/pi
    private static readonly double Two129 = SM.ScaleB(1.0, 129);   // 2**129
    private static readonly double Two302 = SM.ScaleB(1.0, 302);   // 2**302
    private static readonly double MaxFloat64Half = double.MaxValue / 2;

    // ---- order zero -------------------------------------------------------

    public static double J0(double x)
    {
        const double TwoM13 = 1.0 / (1 << 13);
        const double TwoM27 = 1.0 / (1 << 27);
        const double R02 = 1.56249999999999947958e-02, R03 = -1.89979294238854721751e-04,
                     R04 = 1.82954049532700665670e-06, R05 = -4.61832688532103189199e-09,
                     S01 = 1.56191029464890010492e-02, S02 = 1.16926784663337450260e-04,
                     S03 = 5.13546550207318111446e-07, S04 = 1.16614003333790000205e-09;
        if (double.IsNaN(x)) return x;
        if (double.IsInfinity(x)) return 0;
        if (x == 0) return 1;

        x = SM.Abs(x);
        if (x >= 2)
        {
            double s = SM.Sin(x), c = SM.Cos(x);
            double ss = s - c, cc = s + c;
            if (x < MaxFloat64Half)
            {
                double zz = -SM.Cos(x + x);
                if (s * c < 0) cc = zz / ss; else ss = zz / cc;
            }
            double z;
            if (x > Two129) z = OneOverSqrtPi * cc / SM.Sqrt(x);
            else { double u = Pzero(x), v = Qzero(x); z = OneOverSqrtPi * (u * cc - v * ss) / SM.Sqrt(x); }
            return z;
        }
        if (x < TwoM13) { if (x < TwoM27) return 1; return 1 - 0.25 * x * x; }
        double zz2 = x * x;
        double r = zz2 * (R02 + zz2 * (R03 + zz2 * (R04 + zz2 * R05)));
        double sd = 1 + zz2 * (S01 + zz2 * (S02 + zz2 * (S03 + zz2 * S04)));
        if (x < 1) return 1 + zz2 * (-0.25 + (r / sd));
        double up = 0.5 * x;
        return (1 + up) * (1 - up) + zz2 * (r / sd);
    }

    public static double Y0(double x)
    {
        const double TwoM27 = 1.0 / (1 << 27);
        const double U00 = -7.38042951086872317523e-02, U01 = 1.76666452509181115538e-01,
                     U02 = -1.38185671945596898896e-02, U03 = 3.47453432093683650238e-04,
                     U04 = -3.81407053724364161125e-06, U05 = 1.95590137035022920206e-08,
                     U06 = -3.98205194132103398453e-11,
                     V01 = 1.27304834834123699328e-02, V02 = 7.60068627350353253702e-05,
                     V03 = 2.59150851840457805467e-07, V04 = 4.41110311332675467403e-10;
        if (x < 0 || double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return 0;
        if (x == 0) return double.NegativeInfinity;

        if (x >= 2)
        {
            double s = SM.Sin(x), c = SM.Cos(x);
            double ss = s - c, cc = s + c;
            if (x < MaxFloat64Half)
            {
                double zz = -SM.Cos(x + x);
                if (s * c < 0) cc = zz / ss; else ss = zz / cc;
            }
            double z;
            if (x > Two129) z = OneOverSqrtPi * ss / SM.Sqrt(x);
            else { double u = Pzero(x), v = Qzero(x); z = OneOverSqrtPi * (u * ss + v * cc) / SM.Sqrt(x); }
            return z;
        }
        if (x <= TwoM27) return U00 + TwoOverPi * SM.Log(x);
        double zz2 = x * x;
        double uu = U00 + zz2 * (U01 + zz2 * (U02 + zz2 * (U03 + zz2 * (U04 + zz2 * (U05 + zz2 * U06)))));
        double vv = 1 + zz2 * (V01 + zz2 * (V02 + zz2 * (V03 + zz2 * V04)));
        return uu / vv + TwoOverPi * J0(x) * SM.Log(x);
    }

    static readonly double[] p0R8 = { 0.0, -7.03124999999900357484e-02, -8.08167041275349795626e+00, -2.57063105679704847262e+02, -2.48521641009428822144e+03, -5.25304380490729545272e+03 };
    static readonly double[] p0S8 = { 1.16534364619668181717e+02, 3.83374475364121826715e+03, 4.05978572648472545552e+04, 1.16752972564375915681e+05, 4.76277284146730962675e+04 };
    static readonly double[] p0R5 = { -1.14125464691894502584e-11, -7.03124940873599280078e-02, -4.15961064470587782438e+00, -6.76747652265167261021e+01, -3.31231299649172967747e+02, -3.46433388365604912451e+02 };
    static readonly double[] p0S5 = { 6.07539382692300335975e+01, 1.05125230595704579173e+03, 5.97897094333855784498e+03, 9.62544514357774460223e+03, 2.40605815922939109441e+03 };
    static readonly double[] p0R3 = { -2.54704601771951915620e-09, -7.03119616381481654654e-02, -2.40903221549529611423e+00, -2.19659774734883086467e+01, -5.80791704701737572236e+01, -3.14479470594888503854e+01 };
    static readonly double[] p0S3 = { 3.58560338055209726349e+01, 3.61513983050303863820e+02, 1.19360783792111533330e+03, 1.12799679856907414432e+03, 1.73580930813335754692e+02 };
    static readonly double[] p0R2 = { -8.87534333032526411254e-08, -7.03030995483624743247e-02, -1.45073846780952986357e+00, -7.63569613823527770791e+00, -1.11931668860356747786e+01, -3.23364579351335335033e+00 };
    static readonly double[] p0S2 = { 2.22202997532088808441e+01, 1.36206794218215208048e+02, 2.70470278658083486789e+02, 1.53875394208320329881e+02, 1.46576176948256193810e+01 };

    static double Pzero(double x)
    {
        double[] p, q;
        if (x >= 8) { p = p0R8; q = p0S8; }
        else if (x >= 4.5454) { p = p0R5; q = p0S5; }
        else if (x >= 2.8571) { p = p0R3; q = p0S3; }
        else { p = p0R2; q = p0S2; }
        double z = 1 / (x * x);
        double r = p[0] + z * (p[1] + z * (p[2] + z * (p[3] + z * (p[4] + z * p[5]))));
        double s = 1 + z * (q[0] + z * (q[1] + z * (q[2] + z * (q[3] + z * q[4]))));
        return 1 + r / s;
    }

    static readonly double[] q0R8 = { 0.0, 7.32421874999935051953e-02, 1.17682064682252693899e+01, 5.57673380256401856059e+02, 8.85919720756468632317e+03, 3.70146267776887834771e+04 };
    static readonly double[] q0S8 = { 1.63776026895689824414e+02, 8.09834494656449805916e+03, 1.42538291419120476348e+05, 8.03309257119514397345e+05, 8.40501579819060512818e+05, -3.43899293537866615225e+05 };
    static readonly double[] q0R5 = { 1.84085963594515531381e-11, 7.32421766612684765896e-02, 5.83563508962056953777e+00, 1.35111577286449829671e+02, 1.02724376596164097464e+03, 1.98997785864605384631e+03 };
    static readonly double[] q0S5 = { 8.27766102236537761883e+01, 2.07781416421392987104e+03, 1.88472887785718085070e+04, 5.67511122894947329769e+04, 3.59767538425114471465e+04, -5.35434275601944773371e+03 };
    static readonly double[] q0R3 = { 4.37741014089738620906e-09, 7.32411180042911447163e-02, 3.34423137516170720929e+00, 4.26218440745412650017e+01, 1.70808091340565596283e+02, 1.66733948696651168575e+02 };
    static readonly double[] q0S3 = { 4.87588729724587182091e+01, 7.09689221056606015736e+02, 3.70414822620111362994e+03, 6.46042516752568917582e+03, 2.51633368920368957333e+03, -1.49247451836156386662e+02 };
    static readonly double[] q0R2 = { 1.50444444886983272379e-07, 7.32234265963079278272e-02, 1.99819174093815998816e+00, 1.44956029347885735348e+01, 3.16662317504781540833e+01, 1.62527075710929267416e+01 };
    static readonly double[] q0S2 = { 3.03655848355219184498e+01, 2.69348118608049844624e+02, 8.44783757595320139444e+02, 8.82935845112488550512e+02, 2.12666388511798828631e+02, -5.31095493882666946917e+00 };

    static double Qzero(double x)
    {
        double[] p, q;
        if (x >= 8) { p = q0R8; q = q0S8; }
        else if (x >= 4.5454) { p = q0R5; q = q0S5; }
        else if (x >= 2.8571) { p = q0R3; q = q0S3; }
        else { p = q0R2; q = q0S2; }
        double z = 1 / (x * x);
        double r = p[0] + z * (p[1] + z * (p[2] + z * (p[3] + z * (p[4] + z * p[5]))));
        double s = 1 + z * (q[0] + z * (q[1] + z * (q[2] + z * (q[3] + z * (q[4] + z * q[5])))));
        return (-0.125 + r / s) / x;
    }

    // ---- order one --------------------------------------------------------

    public static double J1(double x)
    {
        const double TwoM27 = 1.0 / (1 << 27);
        const double R00 = -6.25000000000000000000e-02, R01 = 1.40705666955189706048e-03,
                     R02 = -1.59955631084035597520e-05, R03 = 4.96727999609584448412e-08,
                     S01 = 1.91537599538363460805e-02, S02 = 1.85946785588630915560e-04,
                     S03 = 1.17718464042623683263e-06, S04 = 5.04636257076217042715e-09,
                     S05 = 1.23542274426137913908e-11;
        if (double.IsNaN(x)) return x;
        if (double.IsInfinity(x) || x == 0) return 0;

        bool sign = false;
        if (x < 0) { x = -x; sign = true; }
        if (x >= 2)
        {
            double s = SM.Sin(x), c = SM.Cos(x);
            double ss = -s - c, cc = s - c;
            if (x < MaxFloat64Half)
            {
                double zz = SM.Cos(x + x);
                if (s * c > 0) cc = zz / ss; else ss = zz / cc;
            }
            double z;
            if (x > Two129) z = OneOverSqrtPi * cc / SM.Sqrt(x);
            else { double u = Pone(x), v = Qone(x); z = OneOverSqrtPi * (u * cc - v * ss) / SM.Sqrt(x); }
            return sign ? -z : z;
        }
        if (x < TwoM27) return 0.5 * x;
        double zz2 = x * x;
        double r = zz2 * (R00 + zz2 * (R01 + zz2 * (R02 + zz2 * R03)));
        double sd = 1.0 + zz2 * (S01 + zz2 * (S02 + zz2 * (S03 + zz2 * (S04 + zz2 * S05))));
        r *= x;
        double zr = 0.5 * x + r / sd;
        return sign ? -zr : zr;
    }

    public static double Y1(double x)
    {
        const double TwoM54 = 1.0 / (1L << 54);
        const double U00 = -1.96057090646238940668e-01, U01 = 5.04438716639811282616e-02,
                     U02 = -1.91256895875763547298e-03, U03 = 2.35252600561610495928e-05,
                     U04 = -9.19099158039878874504e-08,
                     V00 = 1.99167318236649903973e-02, V01 = 2.02552581025135171496e-04,
                     V02 = 1.35608801097516229404e-06, V03 = 6.22741452364621501295e-09,
                     V04 = 1.66559246207992079114e-11;
        if (x < 0 || double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return 0;
        if (x == 0) return double.NegativeInfinity;

        if (x >= 2)
        {
            double s = SM.Sin(x), c = SM.Cos(x);
            double ss = -s - c, cc = s - c;
            if (x < MaxFloat64Half)
            {
                double zz = SM.Cos(x + x);
                if (s * c > 0) cc = zz / ss; else ss = zz / cc;
            }
            double z;
            if (x > Two129) z = OneOverSqrtPi * ss / SM.Sqrt(x);
            else { double u = Pone(x), v = Qone(x); z = OneOverSqrtPi * (u * ss + v * cc) / SM.Sqrt(x); }
            return z;
        }
        if (x <= TwoM54) return -TwoOverPi / x;
        double zz2 = x * x;
        double uu = U00 + zz2 * (U01 + zz2 * (U02 + zz2 * (U03 + zz2 * U04)));
        double vv = 1 + zz2 * (V00 + zz2 * (V01 + zz2 * (V02 + zz2 * (V03 + zz2 * V04))));
        return x * (uu / vv) + TwoOverPi * (J1(x) * SM.Log(x) - 1 / x);
    }

    static readonly double[] p1R8 = { 0.0, 1.17187499999988647970e-01, 1.32394806593073575129e+01, 4.12051854307378562225e+02, 3.87474538913960532227e+03, 7.91447954031891731574e+03 };
    static readonly double[] p1S8 = { 1.14207370375678408436e+02, 3.65093083420853463394e+03, 3.69562060269033463555e+04, 9.76027935934950801311e+04, 3.08042720627888811578e+04 };
    static readonly double[] p1R5 = { 1.31990519556243522749e-11, 1.17187493190614097638e-01, 6.80275127868432871736e+00, 1.08308182990189109773e+02, 5.17636139533199752805e+02, 5.28715201363337541807e+02 };
    static readonly double[] p1S5 = { 5.92805987221131331921e+01, 9.91401418733614377743e+02, 5.35326695291487976647e+03, 7.84469031749551231769e+03, 1.50404688810361062679e+03 };
    static readonly double[] p1R3 = { 3.02503916137373618024e-09, 1.17186865567253592491e-01, 3.93297750033315640650e+00, 3.51194035591636932736e+01, 9.10550110750781271918e+01, 4.85590685197364919645e+01 };
    static readonly double[] p1S3 = { 3.47913095001251519989e+01, 3.36762458747825746741e+02, 1.04687139975775130551e+03, 8.90811346398256432622e+02, 1.03787932439639277504e+02 };
    static readonly double[] p1R2 = { 1.07710830106873743082e-07, 1.17176219462683348094e-01, 2.36851496667608785174e+00, 1.22426109148261232917e+01, 1.76939711271687727390e+01, 5.07352312588818499250e+00 };
    static readonly double[] p1S2 = { 2.14364859363821409488e+01, 1.25290227168402751090e+02, 2.32276469057162813669e+02, 1.17679373287147100768e+02, 8.36463893371618283368e+00 };

    static double Pone(double x)
    {
        double[] p, q;
        if (x >= 8) { p = p1R8; q = p1S8; }
        else if (x >= 4.5454) { p = p1R5; q = p1S5; }
        else if (x >= 2.8571) { p = p1R3; q = p1S3; }
        else { p = p1R2; q = p1S2; }
        double z = 1 / (x * x);
        double r = p[0] + z * (p[1] + z * (p[2] + z * (p[3] + z * (p[4] + z * p[5]))));
        double s = 1.0 + z * (q[0] + z * (q[1] + z * (q[2] + z * (q[3] + z * q[4]))));
        return 1 + r / s;
    }

    static readonly double[] q1R8 = { 0.0, -1.02539062499992714161e-01, -1.62717534544589987888e+01, -7.59601722513950107896e+02, -1.18498066702429587167e+04, -4.84385124285750353010e+04 };
    static readonly double[] q1S8 = { 1.61395369700722909556e+02, 7.82538599923348465381e+03, 1.33875336287249578163e+05, 7.19657723683240939863e+05, 6.66601232617776375264e+05, -2.94490264303834643215e+05 };
    static readonly double[] q1R5 = { -2.08979931141764104297e-11, -1.02539050241375426231e-01, -8.05644828123936029840e+00, -1.83669607474888380239e+02, -1.37319376065508163265e+03, -2.61244440453215656817e+03 };
    static readonly double[] q1S5 = { 8.12765501384335777857e+01, 1.99179873460485964642e+03, 1.74684851924908907677e+04, 4.98514270910352279316e+04, 2.79480751638918118260e+04, -4.71918354795128470869e+03 };
    static readonly double[] q1R3 = { -5.07831226461766561369e-09, -1.02537829820837089745e-01, -4.61011581139473403113e+00, -5.78472216562783643212e+01, -2.28244540737631695038e+02, -2.19210128478909325622e+02 };
    static readonly double[] q1S3 = { 4.76651550323729509273e+01, 6.73865112676699709482e+02, 3.38015286679526343505e+03, 5.54772909720722782367e+03, 1.90311919338810798763e+03, -1.35201191444307340817e+02 };
    static readonly double[] q1R2 = { -1.78381727510958865572e-07, -1.02517042607985553460e-01, -2.75220568278187460720e+00, -1.96636162643703720221e+01, -4.23253133372830490089e+01, -2.13719211703704061733e+01 };
    static readonly double[] q1S2 = { 2.95333629060523854548e+01, 2.52981549982190529136e+02, 7.57502834868645436472e+02, 7.39393205320467245656e+02, 1.55949003336666123687e+02, -4.95949898822628210127e+00 };

    static double Qone(double x)
    {
        double[] p, q;
        if (x >= 8) { p = q1R8; q = q1S8; }
        else if (x >= 4.5454) { p = q1R5; q = q1S5; }
        else if (x >= 2.8571) { p = q1R3; q = q1S3; }
        else { p = q1R2; q = q1S2; }
        double z = 1 / (x * x);
        double r = p[0] + z * (p[1] + z * (p[2] + z * (p[3] + z * (p[4] + z * p[5]))));
        double s = 1 + z * (q[0] + z * (q[1] + z * (q[2] + z * (q[3] + z * (q[4] + z * q[5])))));
        return (0.375 + r / s) / x;
    }

    // ---- order n ----------------------------------------------------------

    public static double Jn(long nl, double x)
    {
        const double TwoM29 = 1.0 / (1 << 29);
        int n = (int)nl;
        if (double.IsNaN(x)) return x;
        if (double.IsInfinity(x)) return 0;

        if (n == 0) return J0(x);
        if (x == 0) return 0;
        if (n < 0) { n = -n; x = -x; }
        if (n == 1) return J1(x);
        bool sign = false;
        if (x < 0) { x = -x; if ((n & 1) == 1) sign = true; }
        double b;
        if ((double)n <= x)
        {
            if (x >= Two302)
            {
                double s = SM.Sin(x), c = SM.Cos(x);
                double temp = 0;
                switch (n & 3)
                {
                    case 0: temp = c + s; break;
                    case 1: temp = -c + s; break;
                    case 2: temp = -c - s; break;
                    case 3: temp = c - s; break;
                }
                b = OneOverSqrtPi * temp / SM.Sqrt(x);
            }
            else
            {
                b = J1(x);
                double a = J0(x);
                for (int i = 1; i < n; i++) { double t0 = b; b = b * ((double)(i + i) / x) - a; a = t0; }
            }
        }
        else
        {
            if (x < TwoM29)
            {
                if (n > 33) b = 0;
                else
                {
                    double temp = x * 0.5;
                    b = temp;
                    double a = 1.0;
                    for (int i = 2; i <= n; i++) { a *= (double)i; b *= temp; }
                    b /= a;
                }
            }
            else
            {
                double w = (double)(n + n) / x;
                double h = 2 / x;
                double q0 = w;
                double z = w + h;
                double q1 = w * z - 1;
                int k = 1;
                while (q1 < 1e9) { k++; z += h; double t0 = q1; q1 = z * q1 - q0; q0 = t0; }
                int m = n + n;
                double t = 0.0;
                for (int i = 2 * (n + k); i >= m; i -= 2) t = 1 / ((double)i / x - t);
                double a = t;
                b = 1;
                double tmp = (double)n;
                double v = 2 / x;
                tmp = tmp * SM.Log(SM.Abs(v * tmp));
                if (tmp < 7.09782712893383973096e+02)
                {
                    for (int i = n - 1; i > 0; i--) { double di = (double)(i + i); double t0 = b; b = b * di / x - a; a = t0; }
                }
                else
                {
                    for (int i = n - 1; i > 0; i--)
                    {
                        double di = (double)(i + i); double t0 = b; b = b * di / x - a; a = t0;
                        if (b > 1e100) { a /= b; t /= b; b = 1; }
                    }
                }
                b = t * J0(x) / b;
            }
        }
        return sign ? -b : b;
    }

    public static double Yn(long nl, double x)
    {
        int n = (int)nl;
        if (x < 0 || double.IsNaN(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return 0;

        if (n == 0) return Y0(x);
        if (x == 0)
        {
            if (n < 0 && (n & 1) == 1) return double.PositiveInfinity;
            return double.NegativeInfinity;
        }
        bool sign = false;
        if (n < 0) { n = -n; if ((n & 1) == 1) sign = true; }
        if (n == 1) return sign ? -Y1(x) : Y1(x);
        double b;
        if (x >= Two302)
        {
            double s = SM.Sin(x), c = SM.Cos(x);
            double temp = 0;
            switch (n & 3)
            {
                case 0: temp = s - c; break;
                case 1: temp = -s - c; break;
                case 2: temp = -s + c; break;
                case 3: temp = s + c; break;
            }
            b = OneOverSqrtPi * temp / SM.Sqrt(x);
        }
        else
        {
            double a = Y0(x);
            b = Y1(x);
            for (int i = 1; i < n && !double.IsNegativeInfinity(b); i++) { double t0 = b; b = ((double)(i + i) / x) * b - a; a = t0; }
        }
        return sign ? -b : b;
    }
}
