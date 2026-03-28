"""
Rational-polynomial approximations.
"""

import functools
import itertools
import math

import numpy as np
import scipy

__all__ = [
    "yup_all_ones", "YupAllOnes",
    "abs_error", "rel_error",
    "evaluator_abs_only", "evaluator_rel_only", "evaluator_max",
        "EvaluatorBalanced",
    "Polynomial", "RationalPolynomial", "RationalPolynomialSum",
    "LookupTable",
]


@functools.cache
def num_coeffs(ndim, degree):
    """
    Returns the number of coefficients of a `ndim`-dimensional `degree`-degree
    polynomial.
    """
    return math.comb(degree + ndim, degree)
@functools.cache
def degreeof(ndim, i):
    """
    Returns the degree (sum of exponents) of the given index.
    """
    if ndim == 1:
        return i
    for k in itertools.count(0):
        if num_coeffs(ndim, k) > i:
            return k
@functools.cache
def invdegreeof(ndim, k):
    """
    Returns the smallest `i` s.t. `degreeof(i) == k`. Note that there may be many
    such solutions, this returns the smallest.
    """
    if ndim == 1:
        return k
    for i in itertools.count(0):
        if degreeof(ndim, i) == k:
            return i

def compositions(n, k):
    if k == 1:
        yield (n,)
        return
    for i in range(n + 1):
        for rest in compositions(n - i, k - 1):
            yield (i,) + rest

def yup_all_ones(degree, *coords):
    """
    Evaluates:
        1, x, y, x^2, x*y, y^2, ...
        (for ndim=2, may be differently multivariate)
    For the given x/y/..., and up to the given degree exponent. Note this ravels
    the cordinates and returns a 2D array of [coord index, power index].
    """
    flatcoords = [x.ravel() for x in np.broadcast_arrays(*coords)]
    ndim = len(flatcoords)
    terms = []
    for sumdeg in range(degree + 1):
        for exps in itertools.product(range(sumdeg + 1), repeat=ndim):
            exps = exps[::-1]
            if sum(exps) != sumdeg:
                continue
            term = np.prod([C**e for C, e in zip(flatcoords, exps)], axis=0)
            terms.append(term)
    return np.stack(terms).T

class YupAllOnes:
    def __init__(self, *coords):
        self.ndim = len(coords)
        self.coords = [x.ravel() for x in coords]
        self.ones = None
        self.maxdegree = -1
    def get_for_idxs(self, pidxs, qidxs):
        maxidx = max(pidxs)
        maxidx = max(maxidx, max(qidxs))
        degree = degreeof(self.ndim, maxidx)
        return self.get(degree)
    def get(self, degree):
        # dont evaluate super low degree, just bump up.
        degree = max(degree, 6 // self.ndim)
        if degree > self.maxdegree:
            self.ones = yup_all_ones(degree, *self.coords)
            self.maxdegree = degree
        return self.ones


def poly_ordering(ndim, degree):
    """
    Returns a list of strings of the variable for each term:
        ["", "x", "y", "x^2", "xy", "y^2", ...]
        (for ndim=2, may be differently multivariate)
    """
    if ndim > 3:
        raise ValueError("havent thought that far ahead")
    coords = "xyz"[:ndim]
    terms = []
    def tostr(c, e):
        if e == 0:
            return ""
        if e == 1:
            return f"{c}"
        return f"{c}^{e} "
    for sumdeg in range(degree + 1):
        for exps in itertools.product(range(sumdeg + 1), repeat=ndim):
            exps = exps[::-1]
            if sum(exps) != sumdeg:
                continue
            term = "".join(tostr(c, e) for c, e in zip(coords, exps))
            terms.append(term.strip())
    return terms

def broadcasted_shape(*coords):
    return np.broadcast_shapes(*[np.asarray(c).shape for c in coords])



def rel_error(real_values, values):
    return (values - real_values) / (real_values.max() - real_values.min())
def abs_error(real_values, values):
    with np.errstate(divide="ignore"):
        return values / real_values - 1

def evaluator_abs_only(real_values, values):
    error = abs_error(real_values, values)
    return float(np.abs(error).max())

def evaluator_rel_only(real_values, values):
    error = rel_error(real_values, values)
    return float(np.abs(error).max())

def evaluator_max(real_values, values):
    absolute = abs_error(real_values, values)
    relative = rel_error(real_values, values)
    error = max(np.abs(absolute).max(), np.abs(relative).max())
    return float(error)

class EvaluatorBalanced:
    def __init__(self, weight_abs=1.0, weight_rel=1.0):
        self.weight_abs = weight_abs
        self.weight_rel = weight_rel

    def __call__(self, real_values, values):
        absolute = abs_error(real_values, values)
        relative = rel_error(real_values, values)
        max_abs = np.abs(absolute).max()
        max_rel = np.abs(relative).max()
        error = self.weight_abs * max_abs + self.weight_rel * max_rel
        error /= self.weight_abs + self.weight_rel
        return float(error)




class Polynomial:
    def __init__(self, ndim, idxs, coeffs, check=True):
        """
        ndim .... integer number of inputs.
        idxs .... tuple of integers specifying non-zero coeffs into
                  the infinite poly-ordering.
        coeffs .. coefficient values.
        """
        if check:
            assert idxs == type(idxs)(sorted(idxs))
            assert all(idx >= 0 for idx in idxs)
            assert len(coeffs) == len(idxs)
            idxs = list(idxs) # list to make numpy slicing work
            coeffs = np.array(coeffs)
        self.ndim = ndim
        self.degree = degreeof(ndim, idxs[-1])
        self.count = len(idxs)
        self.countall = num_coeffs(ndim, self.degree)
        self.idxs = idxs
        self.coeffs = coeffs

    @classmethod
    def one(cls, ndim=1):
        return cls(ndim, [0], [1.0])
    @classmethod
    def constant(cls, const, ndim=1):
        return cls(ndim, [0], [const])
    @classmethod
    def linear(cls, m, c, ndim=1, dim=0):
        return cls(ndim, [0, dim + 1], [c, m])
    @classmethod
    def quadratic(cls, a, b, c):
        return cls(1, [0, 1, 2], [c, b, a])

    def __call__(self, *coords):
        assert self.ndim == len(coords)
        ones = yup_all_ones(self.degree, *coords)
        values = self.eval_ones(ones)
        return values.reshape(broadcasted_shape(*coords))
    def eval_ones(self, ones):
        return ones[:, self.idxs] @ self.coeffs

    def add(self, other):
        assert self.ndim == other.ndim
        coeffs = {i: c for i, c in zip(self.idxs, self.coeffs)}
        for i, c in zip(other.idxs, other.coeffs):
            if i in coeffs:
                coeffs[i] += c
            else:
                coeffs[i] = c
        idxs = list(coeffs.keys())
        coeffs = np.array(list(coeffs.values()))
        return Polynomial(self.ndim, idxs, coeffs)

    def __repr__(self, short=True):
        allcoeffs = np.zeros(self.countall)
        allcoeffs[self.idxs] = self.coeffs
        variables = poly_ordering(self.ndim, self.countall)
        def mul(c, x):
            if c == 0:
                return ""
            if c == 1 and x:
                return x
            c = f"{c:.5g}" if short else repr(float(c))
            return f"{c} {x}".strip()
        terms = [mul(c, x) for c, x in zip(allcoeffs, variables)]
        while terms and not terms[0].strip():
            terms = terms[1:]
        def add(t):
            if not t.strip():
                return ""
            if t[0] == "-":
                return f" - {t[1:]}"
            return f" + {t}"
        if not terms:
            return "1"
        terms = terms[0] + "".join(add(t) for t in terms[1:])
        return terms


class RationalPolynomial:
    """
    Represents a rational polynomial of the form:
           p0 + p1 x + p2 x^2 + ... + x^n
        -----------------------------------
         q0 + q1 x + q2 x^2 + ... + qm x^m
    """

    _IDXS_CACHE = {}
    @classmethod
    def _at_cost_one(cls, ndim, cost, blitz=0.0, _cache=True):
        # Yields all 1D index tuples with the given cost, ordered by descending
        # length and then ascending last index. `blitz` may be used to skip
        # extreme index tuples, where a higher blitz corresponds to skipping more
        # and more.

        # Cache small costs.
        if _cache and cost < 20:
            key = (ndim, cost, blitz)
            if key not in cls._IDXS_CACHE:
                it = cls._at_cost_one(ndim, cost, blitz, _cache=False)
                cls._IDXS_CACHE[key] = list(it)
            yield from cls._IDXS_CACHE[key]
            return

        # We can vary degree and num of coeffs. define a cost heuristic as:
        #   ndim * degreeof(max(idxs)) + 2 * len(idxs) - 1
        # since the poly constructs all powers on the way to the highest, and
        # then each term requires 1 mul and 1 add/sub. (note im not accounting
        # for repeated squaring i cannot be fucked). the -1 is just to ensure
        # that the lowest cost tuple of (0,) has a cost of 1. so, cost is
        # entirely determined by length and greatest value. want to go longest to
        # shortest.
        longest = 0
        while ndim * degreeof(ndim, longest) + 2*(longest + 1) - 1 <= cost:
            longest += 1
        for length in range(longest, 0, -1):
            # want to go smallest to greatest.
            #  ndim * degreeof(last) + 2*length - 1 = cost
            #  ndim * degreeof(last) = cost - 2*length + 1
            # may not be solutions of this cost for this length.
            if (cost - 2*length + 1) % ndim:
                continue
            #  degreeof(last) = (cost - 2*length + 1) // ndim
            #  last = invdegreeof((cost - 2*length + 1) // ndim)
            last = invdegreeof(ndim, (cost - 2*length + 1) // ndim) - 1
            while ndim * degreeof(ndim, last + 1) + 2*length - 1 == cost:
                last += 1
                # If blitzing, dont let the powers get too extreme without a lot
                # of other powers around. We only do this when blitzing because
                # it means we may miss cheaper solutions, however those solutions
                # are generally unlikely and by eliminating them we churn through
                # possiblities much faster.
                if blitz > 0:
                    if last / ndim > (1 + 1.0/blitz) * length:
                        break
                for combo in itertools.combinations(range(last), length - 1):
                    yield list(combo) + [last], cost
    @classmethod
    def _all_idxs_one(cls, ndim, min_cost, max_cost, blitz=0.0):
        # Yields all index tuples with a cost in `min_cost`..`max_cost`. `blitz`
        # may be used to skip extreme index tuples, where a higher blitz
        # corresponds to skipping more and more.
        for cost in range(min_cost, max_cost + 1):
            yield from cls._at_cost_one(ndim, cost, blitz)
    @classmethod
    def _all_idxs(cls, ndim, blitz=0.0, starting_cost=2, ending_cost=100000000):
        # Iterate through the rat polys in cost order, where the cost of the rat
        # poly is just the sum of each polys cost.
        for cost in range(starting_cost, ending_cost + 1):
            min_cost = 1
            max_cost = cost - 1
            # If blitzing, don't let the numer/denom cost difference get too
            # extreme (since its more likely to be an accurate approx if not).
            if blitz > 0:
                min_cost = int(max_cost / (2 + 1.0/blitz))
                max_cost -= min_cost
            for pidxs, pcost in cls._all_idxs_one(ndim, min_cost, max_cost,
                    blitz):
                for qidxs, _ in cls._at_cost_one(ndim, cost - pcost, blitz):
                    # if the numerator and denominator both dont have constants,
                    # x can be factored from both.
                    #  (x + x^2) / x == 1 + x
                    # for higher dimensions, we dont bother checking but the same
                    # factorisation can be applied to find redundant options.
                    if ndim == 1 and pidxs[0] != 0 and qidxs[0] != 0:
                        continue
                    # note that while f(x)/c is the same as scaling all
                    # coefficients, it isnt the same for us since we fix the
                    # highest index numerator coefficient to 1.
                    # if qidxs == (0,):
                    #     continue
                    yield pidxs, qidxs


    @classmethod
    def approximate(cls, pidxs, qidxs, *points):
        ndim = len(points) - 1
        assert ndim >= 1
        assert all(x.ndim == 1 for x in points)
        coords = points[:-1]
        real_values = points[-1]
        ones = YupAllOnes(*coords).get_for_idxs(pidxs, qidxs)
        return cls.approximate_ones(ndim, pidxs, qidxs, ones, real_values)

    @classmethod
    def approximate_ones(cls, ndim, pidxs, qidxs, ones, real_values):
        pidxs = list(pidxs)
        qidxs = list(qidxs)
        assert ndim >= 1
        assert real_values.ndim == 1

        # implicitly set the lowest power coeff in the denom to 1.
        def initial_state():
            return np.zeros(len(pidxs) + len(qidxs) - 1)
        def get_coeffs(state):
            pcoeffs = state[:len(pidxs)]
            qcoeffs = state[len(pidxs):]
            qcoeffs = np.concatenate(([1.0], qcoeffs))
            return pcoeffs, qcoeffs

        def jacobian(state):
            pcoeffs, qcoeffs = get_coeffs(state)
            P = ones[:, pidxs] @ pcoeffs
            Q = ones[:, qidxs] @ qcoeffs
            N = len(pidxs)
            M = len(qidxs) - 1
            J = np.zeros((ones.shape[0], N + M))
            J[:, :N] = -ones[:, pidxs] / Q[:, None]
            J[:, N:] = (P[:, None] * ones[:, qidxs[1:]]) / (Q[:, None]**2)
            return J

        def residuals(state):
            pcoeffs, qcoeffs = get_coeffs(state)
            P = ones[:, pidxs] @ pcoeffs
            Q = ones[:, qidxs] @ qcoeffs
            with np.errstate(divide="ignore"):
                values = P / Q
            return real_values - values

        # Optimise via least squares, since its significantly more stable than a
        # minimise-max-error optimiser.
        state = initial_state()
        for _ in range(10):
            res = scipy.optimize.least_squares(residuals, state,
                    jac=jacobian, method="lm")
            state = res.x
            if res.status > 0:
                break
        pcoeffs, qcoeffs = get_coeffs(state)
        # Make the top power in the numer have a coeff of 1.
        factor = pcoeffs[-1]
        if abs(factor) > 1e-8:
            pcoeffs /= factor
            qcoeffs /= factor
        ratpoly = RationalPolynomial(
            Polynomial(ndim, pidxs, pcoeffs),
            Polynomial(ndim, qidxs, qcoeffs),
        )
        values = ratpoly.eval_ones(ones)
        return ratpoly, values

    @classmethod
    def search_forwards(cls, *points, blitz=0.0, starting_cost=2,
            evaluator=evaluator_abs_only, padto=40, print_all_below=0.01):
        ndim = len(points) - 1
        assert ndim >= 1
        assert all(x.ndim == 1 for x in points)
        coords = points[:-1]
        real_values = points[-1]
        yao = YupAllOnes(*coords)
        best_error = float("inf")
        last_length = 0
        for pidxs, qidxs in cls._all_idxs(ndim, blitz, starting_cost):
            s = f"{pidxs}, {qidxs}"
            print(s + " " * (last_length - len(s)), end="\r")
            last_length = len(s)

            ones = yao.get_for_idxs(pidxs, qidxs)
            ratpoly, values = cls.approximate_ones(ndim, pidxs, qidxs, ones,
                    real_values)
            error = evaluator(real_values, values)

            if error <= max(best_error, print_all_below):
                s = f"{s} .."
                s += "." * (padto - len(s))
                s += f" {100 * error:.4g}%"
                s += " *" * (error <= best_error)
                s += " " * (len(s) - last_length)
                print(s)
                last_length = 0
            best_error = min(best_error, error)

    @classmethod
    def search_backwards(cls, *points, evaluator=evaluator_abs_only, padto=40,
            max_error=0.01):
        ndim = len(points) - 1
        assert ndim >= 1
        assert all(x.ndim == 1 for x in points)
        coords = points[:-1]
        real_values = points[-1]
        yao = YupAllOnes(*coords)
        done = set()
        for total_degree in itertools.count(2):
            leave = total_degree//4
            for pdegree in range(1 + leave, total_degree - leave):
                pidxs = list(range(pdegree))
                qidxs = list(range(total_degree - pdegree))
                cls._search_backwards_branch(pidxs, qidxs, yao, real_values,
                        evaluator, padto, max_error, done)

    @classmethod
    def _search_backwards_branch(cls, pidxs, qidxs, yao, real_values, evaluator,
            padto, max_error, done):
        N = len(pidxs) + len(qidxs)
        for n in range((N - 1) // 6):
            best_error = float("inf")
            best_idxs = None
            last_length = 0
            for idxs in itertools.combinations(range(N), n):
                pi = [pidxs[i] for i in range(len(pidxs))
                      if i not in idxs]
                qi = [qidxs[i] for i in range(len(qidxs))
                      if (i + len(pidxs)) not in idxs]
                if not pi or not qi:
                    continue

                key = tuple(pi + qi)
                if key in done:
                    continue
                done.add(key)

                s = f"{pi}, {qi}"
                print(s + " " * (last_length - len(s)), end="\r")
                last_length = len(s)

                ones = yao.get_for_idxs(pi, qi)
                ratpoly, values = cls.approximate_ones(yao.ndim, pi, qi, ones,
                        real_values)
                error = evaluator(real_values, values)
                if error < best_error:
                    best_error = error
                    best_idxs = idxs
            if best_idxs is None:
                return

            # Pick best.
            pi = [pidxs[i] for i in range(len(pidxs))
                  if i not in best_idxs]
            qi = [qidxs[i] for i in range(len(qidxs))
                  if (i + len(pidxs)) not in best_idxs]

            if best_error <= max_error:
                s = f"{pi}, {qi} .."
                s += "." * (padto - len(s))
                s += f" {100 * best_error:.4g}%"
                s += " " * (len(s) - last_length)
                print(s)
                last_length = 0
            else:
                print(" " * last_length, end="\r")
                last_length = 0

            if best_error <= max_error * 1.1:
                cls._search_backwards_branch(pi, qi, yao, real_values, evaluator,
                        padto, max_error, done)
            else:
                return


    def __init__(self, p, q):
        assert p.ndim == q.ndim
        self.p = p
        self.q = q
        self.ndim = p.ndim
        self.maxdegree = max(p.degree, q.degree)

    def __call__(self, *coords):
        assert self.ndim == len(coords)
        ones = yup_all_ones(self.maxdegree, *coords)
        values = self.eval_ones(ones)
        return values.reshape(broadcasted_shape(*coords))
    def eval_ones(self, ones):
        P = ones[:, self.p.idxs] @ self.p.coeffs
        Q = ones[:, self.q.idxs] @ self.q.coeffs
        return P / Q

    def __repr__(self, short=True):
        return f"({self.p.__repr__(short)}) / ({self.q.__repr__(short)})"

    def code(self):
        p = self.p
        q = self.q
        allidxs = set(p.idxs) | set(q.idxs)
        ndim = self.ndim
        degree = max(p.degree, q.degree)
        ftoa = lambda x: "+"*bool(x>=0.0) + repr(float(x))
        s = ""

        def poly(ones, cs, xs):
            parts = []
            for one, c, x in zip(ones, cs, xs):
                pre = "+ " * (not not parts)
                if not x:
                    parts.append(f"{pre}{c} ")
                else:
                    if one:
                        parts.append(f"{pre}{x} ")
                    else:
                        parts.append(f"{pre}{c}*{x} ")
            return "".join(parts).strip()

        # Make all powers.
        xnames = {}
        i = -1
        for sumdeg in range(degree + 1):
            for exps in itertools.product(range(sumdeg + 1), repeat=ndim):
                exps = exps[::-1]
                if sum(exps) != sumdeg:
                    continue
                i += 1
                if sumdeg > 1 and i not in allidxs:
                    continue
                exps += (0, ) * (3 - len(exps))
                parts = []
                parts += [f"x{exps[0]}"] * (exps[0] > 0)
                parts += [f"y{exps[1]}"] * (exps[1] > 0)
                parts += [f"z{exps[2]}"] * (exps[2] > 0)
                name = "".join(parts)
                xnames[i] = name
                if name:
                    s += f"    f64 {name} = ;\n"

        if q.idxs == [0]:
            coeffs = p.coeffs / q.coeffs[0]
            # just normal poly.
            ones = []
            for i, coeff in zip(p.idxs, coeffs):
                ones.append(abs(coeff) == 1.0 and xnames[i])
                if ones[-1]:
                    continue
                s += f"    f64 c{i} = {ftoa(coeff)};\n"
            cs = [f"c{i}" for i in p.idxs]
            xs = [xnames[i] for i in p.idxs]
            s += f"    return {poly(ones, cs, xs)};"
            return s
        if p.idxs == [0]:
            # inverse poly.
            numer = float(p.coeffs[0] / q.coeffs[-1])
            coeffs = q.coeffs / q.coeffs[-1]
            ones = []
            s += f"    f64 n0 = {ftoa(numer)};\n"
            for i, coeff in zip(q.idxs, coeffs):
                ones.append(abs(coeff) == 1.0 and xnames[i])
                if ones[-1]:
                    continue
                s += f"    f64 d{i} = {ftoa(coeff)};\n"
            cs = [f"d{i}" for i in q.idxs]
            xs = [xnames[i] for i in q.idxs]
            s += f"    f64 Num = {"-"*(numer < 0)}n0;\n"
            s += f"    f64 Den = {poly(ones, cs, xs)};\n"
            s += f"    return Num / Den;"
            return s

        pones = []
        for i, coeff in zip(p.idxs, p.coeffs):
            pones.append(abs(coeff) == 1.0 and xnames[i])
            if pones[-1]:
                continue
            s += f"    f64 n{i} = {ftoa(coeff)};\n"
        qones = []
        for i, coeff in zip(q.idxs, q.coeffs):
            qones.append(abs(coeff) == 1.0 and xnames[i])
            if qones[-1]:
                continue
            s += f"    f64 d{i} = {ftoa(coeff)};\n"
        pcs = [f"n{i}" for i in p.idxs]
        qcs = [f"d{i}" for i in q.idxs]
        pxs = [xnames[i] for i in p.idxs]
        qxs = [xnames[i] for i in q.idxs]
        s += f"    f64 Num = {poly(pones, pcs, pxs)};\n"
        s += f"    f64 Den = {poly(qones, qcs, qxs)};\n"
        s += f"    return Num / Den;"
        return s



class RationalPolynomialSum:

    @classmethod
    def approximate(cls, idxs, *points):
        L = len(idxs)
        ndim = len(points) - 1
        assert ndim >= 1
        assert all(x.ndim == 1 for x in points)
        coords = points[:-1]
        real_values = points[-1]
        maxidx = 0
        for pidxs, qidxs in idxs:
            maxidx = max(maxidx, max(pidxs))
            maxidx = max(maxidx, max(qidxs))
        maxdegree = degreeof(ndim, maxidx)
        ones = YupAllOnes(*coords).get(maxdegree)
        return cls.multiapproximate_ones(ndim, idxs, ones, real_values)

    @classmethod
    def approximate_ones(cls, ndim, idxs, ones, real_values):
        L = len(idxs)
        assert ndim >= 1
        assert real_values.ndim == 1
        assert L >= 1
        offs = np.empty((L, 2), dtype=int)
        lens = np.empty((L, 2), dtype=int)
        running = 0
        for term, (pidxs, qidxs) in enumerate(idxs):
            offs[term, 0] = running
            lens[term, 0] = len(pidxs)
            running += lens[term, 0]
            offs[term, 1] = running
            lens[term, 1] = len(qidxs) - 1 # implicit leading zero.
            running += lens[term, 1]


        # implicitly set the lowest power coeff in the denom to 1.
        def initial_state():
            return np.zeros(np.sum(lens))
        def get_coeffs(state):
            coeffs = []
            for term in range(L):
                poff = offs[term, 0]
                qoff = offs[term, 1]
                plen = lens[term, 0]
                qlen = lens[term, 1]
                pcoeffs = state[poff:poff + plen]
                qcoeffs = state[qoff:qoff + qlen]
                qcoeffs = np.concatenate(([1.0], qcoeffs))
                coeffs.append((pcoeffs, qcoeffs))
            return coeffs

        def jacobian(state):
            coeffs = get_coeffs(state)
            P = np.zeros(ones.shape[:1])
            Q = np.zeros(ones.shape[:1])
            for (pidxs, qidxs), (pcoeffs, qcoeffs) in zip(idxs, coeffs):
                P += ones[:, pidxs] @ pcoeffs
                Q += ones[:, qidxs] @ qcoeffs
            J = np.empty((ones.shape[0], np.sum(lens)))
            for term, (pidxs, qidxs) in enumerate(idxs):
                poff = offs[term, 0]
                qoff = offs[term, 1]
                plen = lens[term, 0]
                qlen = lens[term, 1]
                Jp = -ones[:, pidxs] / Q[:, None]
                Jq = (P[:, None] * ones[:, qidxs[1:]]) / (Q[:, None]**2)
                J[:, poff:poff + plen] = Jp
                J[:, qoff:qoff + qlen] = Jq
            return J

        def residuals(state):
            values = np.zeros((ones.shape[0],))
            coeffs = get_coeffs(state)
            for (pidxs, qidxs), (pcoeffs, qcoeffs) in zip(idxs, coeffs):
                P = ones[:, pidxs] @ pcoeffs
                Q = ones[:, qidxs] @ qcoeffs
                with np.errstate(divide="ignore"):
                    values += P / Q
            return real_values - values

        state = initial_state()
        for _ in range(10):
            res = scipy.optimize.least_squares(residuals, state, jac=jacobian,
                    method="lm")
            state = res.x
            if res.status > 0:
                break
        coeffs = get_coeffs(state)
        ratpolys = []
        values = np.zeros(ones.shape[0])
        for (pidxs, qidxs), (pcoeffs, qcoeffs) in zip(idxs, coeffs):
            # Make the top power in the numer have a coeff of 1.
            factor = pcoeffs[-1]
            if abs(factor) > 1e-8:
                pcoeffs /= factor
                qcoeffs /= factor
            ratpoly = RationalPolynomial(
                Polynomial(ndim, pidxs, pcoeffs),
                Polynomial(ndim, qidxs, qcoeffs),
            )
            values += ratpoly.eval_ones(ones)
            ratpolys.append(ratpoly)
        return RationalPolynomialSum(ratpolys), values

    @classmethod
    def search_forwards(cls, *points, blitz=0.0, starting_cost=1,
            evaluator=evaluator_abs_only, padto=60, print_all_below=0.01):
        ndim = len(points) - 1
        assert ndim >= 1
        assert all(x.ndim == 1 for x in points)
        coords = points[:-1]
        real_values = points[-1]
        yao = YupAllOnes(*coords)

        def get_tokens(cost):
            cost += 1
            yield from RationalPolynomial._all_idxs(ndim, blitz, cost, cost)
        def allidxs(total_cost):
            # Collect all tokens grouped by cost.
            cost_to_tokens = {}
            for cost in range(1, total_cost + 1):
                tokens = list(get_tokens(cost))
                if tokens:
                    cost_to_tokens[cost] = tokens

            # Flatten into list.
            items = []
            for cost, tokens in cost_to_tokens.items():
                for token in tokens:
                    items.append((cost, token))
            # Sort for deterministic output.
            items.sort()

            # Backtrack to find all combinations.
            penalty = 2.0 # one addition and div.
            minc = int(total_cost * blitz * 0.5 / (blitz + 1.0))
            maxc = total_cost - minc
            def backtrack(start, remaining, current):
                if remaining == 0:
                    yield list(current)
                    return
                if remaining < 0:
                    return

                for i in range(start, len(items)):
                    cost, token = items[i]
                    # Apply penalty for extra terms.
                    extra = penalty if current else 0
                    this = cost + extra
                    # Skip if cost too large
                    if this > remaining:
                        break
                    if this > maxc:
                        break
                    if this < minc:
                        continue
                    current.append(token)
                    yield from backtrack(i + 1, remaining - this, current)
                    current.pop()
            return backtrack(0, total_cost, [])

        best_error = float("inf")
        last_length = 0
        for total_cost in itertools.count(starting_cost):
            for idxs in allidxs(total_cost):
                s = f"{idxs[0][0]}, {idxs[0][1]}"
                for pidxs, qidxs in idxs[1:]:
                    s += f" + {pidxs}, {qidxs}"
                print(s + " " * (last_length - len(s)), end="\r")
                last_length = len(s)

                maxidx = 0
                for pidxs, qidxs in idxs:
                    maxidx = max(maxidx, pidxs[-1])
                    maxidx = max(maxidx, qidxs[-1])
                maxdegree = degreeof(ndim, maxidx)
                ones = yao.get(maxdegree)
                rps, values = cls.approximate_ones(ndim, idxs, ones, real_values)
                error = evaluator(real_values, values)

                if error <= max(best_error, print_all_below):
                    s = f"{s} .."
                    s += "." * (padto - len(s))
                    s += f" {100 * error:.4g}%"
                    s += " *" * (error <= best_error)
                    s += " " * (len(s) - last_length)
                    print(s)
                    last_length = 0
                best_error = min(best_error, error)

    def __init__(self, rps):
        assert len(rps) > 0
        assert all(rps[0].ndim == rp.ndim for rp in rps)
        self.rps = rps
        self.ndim = rps[0].ndim
        self.maxdegree = max(rp.maxdegree for rp in rps)

    def __call__(self, *coords):
        return np.sum(tuple(rp(*coords) for rp in self.rps), axis=0)
    def eval_ones(self, ones):
        return np.sum(tuple(rp.eval_ones(ones) for rp in self.rps), axis=0)

    def __repr__(self, short=True):
        s = ""
        for rp in self.rps:
            if s:
                s += " + "
            s += rp.__repr__(short)
        return s

    def code(self):
        s = "    f64 value = 0.0;\n"
        for rp in self.rps:
            s += "{\n"
            for line in rp.code():
                line = line.replace("return ", "value += ")
                s += f"    {line}"
            s += "}\n"
        s += "    return value;"
        return s





class LookupTable:
    """
    Multi-dimensional lookup table, where each dimension is a table lookup of
    some count, however instead of a purely lerped grid it is pre-biased by a
    rational polynomial. This bias function should return in 0..1 for the
    intended input range, and this value will then be lerped across that table
    dimension (note the 0..1 is not enforced, and factors outside will be
    extrapolated).
    """

    @classmethod
    def approximate_ones(cls, shape, idxs, ones, real_values, f):
        ndim = len(shape)
        assert ndim == len(idxs)
        assert real_values.ndim == 1
        coords = [ones[:, 1 + dim] for dim in range(ndim)]
        bounds = [(c.min(), c.max()) for c in coords]
        N = np.prod(shape)
        offs = np.empty((ndim, 2), dtype=int)
        lens = np.empty((ndim, 2), dtype=int)
        running = N
        maxdegree = 0
        for dim, (pidxs, qidxs) in enumerate(idxs):
            offs[dim, 0] = running
            lens[dim, 0] = len(pidxs)
            running += lens[dim, 0]
            maxdegree = max(maxdegree, max(pidxs))
            offs[dim, 1] = running
            lens[dim, 1] = len(qidxs) - 1 # implicit leading zero.
            running += lens[dim, 1]
            maxdegree = max(maxdegree, max(qidxs))

        def initial_state():
            state = np.zeros(N + np.sum(lens))

            lin_coords = [np.linspace(xmin, xmax, 20) for xmin, xmax in bounds]
            lin_coords = np.meshgrid(*lin_coords)
            lin_ones = yup_all_ones(maxdegree, *lin_coords)
            for dim, (pidxs, qidxs) in enumerate(idxs):
                # fit it to a line from 0-1 in the right dim.
                xmin, xmax = bounds[dim]
                lin = lin_coords[dim]
                lin = ((lin - xmin) / (xmax - xmin)).ravel()
                ratpoly, _ = RationalPolynomial.approximate_ones(ndim, pidxs,
                        qidxs, lin_ones, lin)
                poff = offs[dim, 0]
                qoff = offs[dim, 1]
                plen = lens[dim, 0]
                qlen = lens[dim, 1]
                # Normalise to an implicit 1+denom
                factor = ratpoly.q.coeffs[0]
                if abs(factor) > 1e-8:
                    state[poff:poff + plen] = ratpoly.p.coeffs / factor
                    state[qoff:qoff + qlen] = ratpoly.q.coeffs[1:] / factor
            lins = []
            for dim, (xmin, xmax) in enumerate(bounds):
                lin = np.linspace(xmin, xmax, shape[dim])
                shapeto = [1] * ndim
                shapeto[dim] = shape[dim]
                lin = lin.reshape(shapeto)
                lin = np.broadcast_to(lin, shape)
                lins.append(lin.ravel())
            state[:N] = f(*lins)
            return state

        def get_table(state):
            return state[:N].reshape(shape).astype(np.float32)
        def get_coeffs(state):
            coeffs = []
            for dim in range(ndim):
                poff = offs[dim, 0]
                qoff = offs[dim, 1]
                plen = lens[dim, 0]
                qlen = lens[dim, 1]
                pcoeffs = state[poff:poff + plen]
                qcoeffs = state[qoff:qoff + qlen]
                qcoeffs = np.concatenate(([1.0], qcoeffs))
                coeffs.append((pcoeffs, qcoeffs))
            return coeffs

        def get_obj(state):
            table = get_table(state)
            coeffs = get_coeffs(state)
            lookups = []
            for (pidxs, qidxs), (pcoeffs, qcoeffs) in zip(idxs, coeffs):
                lookups.append(RationalPolynomial(
                    Polynomial(ndim, pidxs, pcoeffs),
                    Polynomial(ndim, qidxs, qcoeffs),
                ))
            return LookupTable(table, *lookups)

        def residuals(state):
            obj = get_obj(state)
            values = obj.eval_ones(ones)
            return real_values - values

        # Optimise via least squares, since its significantly more stable than a
        # minimise-max-error optimiser.
        state = initial_state()
        for _ in range(10):
            res = scipy.optimize.least_squares(residuals, state, method="lm")
            state = res.x
            if res.status > 0:
                break
        lut = get_obj(state)
        values = lut.eval_ones(ones)
        return lut, values

    @classmethod
    def search_forwards(cls, f, *points, evaluator=evaluator_abs_only, padto=60,
            print_all_below=0.01, only_table=(), only_ratpoly=()):
        coords = points[:-1]
        real_values = points[-1]
        yao = YupAllOnes(*coords)
        ndim = len(coords)

        def lengths_at_cost(c):
            # https://www.desmos.com/calculator/tq3ub2frqr
            g = lambda x: int(np.ceil(0.5*(pow(1.5, x + 0.7095113) + x)))
            yield from range(g(c - 1), g(c) + 1)
        def generate_state(mode, costs, shape=(), idxs=()):
            c = 2 + costs[0]
            if mode[0]: # use lookuptable
                idxs += (([0, 1], [0]),)
                alllens = lengths_at_cost(c)
                if len(mode) > 1:
                    for length in alllens:
                        yield from generate_state(mode[1:], costs[1:],
                                shape + (length,), idxs)
                else:
                    for length in alllens:
                        yield shape + (length,), idxs
            else:
                shape += (2,)
                allidxs = RationalPolynomial._all_idxs(ndim,
                        starting_cost=c + 2, ending_cost=c + 2)
                if len(mode) > 1:
                    for idx in allidxs:
                        yield from generate_state(mode[1:], costs[1:], shape,
                                idxs + (idx,))
                else:
                    for idx in allidxs:
                        yield shape, (idxs + (idx,))

        def all_states():
            for total_cost in itertools.count(0):
                for mode in itertools.product([False, True], repeat=ndim):
                    if any(not mode[dim] for dim in only_table):
                        continue
                    if any(mode[dim] for dim in only_ratpoly):
                        continue
                    for costs in compositions(total_cost, ndim):
                        for state in generate_state(mode, costs):
                            yield state

        best_error = float("inf")
        last_length = 0
        for state in all_states():
            s = f"{state}"
            print(s + " " * (last_length - len(s)), end="\r")
            last_length = len(s)

            maxidx = 0
            for pidxs, qidxs in state[1]:
                maxidx = max(maxidx, max(pidxs))
                maxidx = max(maxidx, max(qidxs))
            maxdegree = degreeof(ndim, maxidx)
            ones = yao.get(maxdegree)

            lut, values = cls.approximate_ones(state[0], state[1], ones,
                    real_values, f)
            error = evaluator(real_values, values)

            if error <= max(best_error, print_all_below):
                s = f"{s} .."
                s += "." * (padto - len(s))
                s += f" {100 * error:.4g}%"
                s += " *" * (error <= best_error)
                s += " " * (len(s) - last_length)
                print(s)
                last_length = 0
            best_error = min(best_error, error)


    class linear_lookup:
        def __init__(self, xlo, xhi):
            self.xlo = xlo
            self.xhi = xhi
            self.m = 1/(xhi - xlo)
            self.c = -xlo/(xhi - xlo)
    def __init__(self, table, *lookups):
        assert table.ndim >= 1
        assert table.size >= 1
        assert all(x >= 2 for x in table.shape)
        assert table.dtype == np.float32
        assert len(lookups) == table.ndim
        lookups = list(lookups)
        for i, lookup in enumerate(lookups):
            if isinstance(lookup, LookupTable.linear_lookup):
                lookups[i] = RationalPolynomial(
                    Polynomial.linear(lookup.m, lookup.c, table.ndim, i),
                    Polynomial.one(table.ndim),
                )
        assert all(isinstance(x, RationalPolynomial) for x in lookups)
        assert all(x.ndim == table.ndim for x in lookups)
        self.table = table
        self.lookups = lookups
    @property
    def size(self):
        return self.table.size
    @property
    def shape(self):
        return self.table.shape
    @property
    def ndim(self):
        return self.table.ndim

    def __call__(self, *coords):
        assert self.ndim == len(coords)
        maxdegree = max(x.maxdegree for x in self.lookups)
        ones = yup_all_ones(maxdegree, *coords)
        values = self.eval_ones(ones)
        return values.reshape(broadcasted_shape(*coords))
    def eval_ones(self, ones):
        idxs = np.zeros(shape=(self.ndim, ones.shape[0]), dtype=int)
        lerps = np.empty(shape=(self.ndim, ones.shape[0]), dtype=float)

        for i, lookup in enumerate(self.lookups):
            x = lookup.eval_ones(ones)
            N = self.shape[i]
            if N > 2:
                x *= N - 1
                idxs[i] = np.clip(x.astype(int), 0, N - 2)
                lerps[i] = x - idxs[i].astype(float)
            else:
                assert N == 2
                lerps[i] = x

        ret = np.zeros(shape=(ones.shape[0],), dtype=float)
        for bits in itertools.product([0, 1], repeat=self.ndim):
            idx = []
            weight = 1
            for d, bit in enumerate(bits):
                if bit == 0:
                    idx.append(idxs[d])
                    weight *= (1 - lerps[d])
                else:
                    idx.append(idxs[d] + 1)
                    weight *= lerps[d]
            ret += weight * self.table[tuple(idx)]
        return ret

    def __repr__(self):
        s = f"LookupTable [shape={self.shape}]\n"
        s += str(self.table)
        for i, lookup in enumerate(self.lookups):
            s += f"\nlookup dim {i}: {lookup}"
        return s
