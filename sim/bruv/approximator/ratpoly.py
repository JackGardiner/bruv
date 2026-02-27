"""
Rational-polynomial approximations.
"""

import functools
import itertools
import math

import numpy as np
import scipy

__all__ = [
    "IdxGenerator",
    "abs_error", "rel_error",
    "EvaluatorAbsOnly", "EvaluatorRelOnly", "EvaluatorMax", "EvaluatorBalanced",
    "Printer",
    "Polynomial", "RationalPolynomial",
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



class IdxGenerator:
    _CACHE = {}
    @classmethod
    def at_cost_1D(cls, ndim, cost, blitz=0.0, _cache=True):
        """
        Yields all 1D index tuples with the given cost, ordered by descending
        length and then ascending last index. `blitz` may be used to skip extreme
        index tuples, where a higher blitz corresponds to skipping more and more.
        """

        # Cache small costs.
        if _cache and cost < 20:
            key = (ndim, cost, blitz)
            if key not in cls._CACHE:
                it = cls.at_cost_1D(ndim, cost, blitz, _cache=False)
                cls._CACHE[key] = list(it)
            yield from cls._CACHE[key]
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
    def all_idxs_1D(cls, ndim, min_cost, max_cost, blitz=0.0):
        """
        Yields all index tuples with a cost in `min_cost`..`max_cost`. `blitz`
        may be used to skip extreme index tuples, where a higher blitz
        corresponds to skipping more and more.
        """
        for cost in range(min_cost, max_cost + 1):
            yield from cls.at_cost_1D(ndim, cost, blitz)

    @classmethod
    def infinite(cls, ndim, blitz=0.0, starting_cost=2):
        # Iterate through the rat polys in cost order, where the cost of the rat
        # poly is just the sum of each polys cost.
        for cost in itertools.count(starting_cost):
            min_cost = 1
            max_cost = cost - 1
            # If blitzing, don't let the numer/denom cost difference get too
            # extreme (since its more likely to be an accurate approx if not).
            if blitz > 0:
                min_cost = int(max_cost / (2 + 1.0/blitz))
                max_cost -= min_cost
            for pidxs, pcost in cls.all_idxs_1D(ndim, min_cost, max_cost, blitz):
                for qidxs, _ in cls.at_cost_1D(ndim, cost - pcost, blitz):
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
    def just(cls, pidxs, qidxs):
        yield list(pidxs), list(qidxs)



def rel_error(real_values, values):
    return (values - real_values) / (real_values.max() - real_values.min())
def abs_error(real_values, values):
    with np.errstate(divide="ignore"):
        return values / real_values - 1

class EvaluatorBase:
    def __init__(self, leave_when_better_than=None):
        self.leave_when_better_than = leave_when_better_than

    def _get_ret(self, error):
        leave = False
        if self.leave_when_better_than is not None:
            leave = error < self.leave_when_better_than
        return error, leave

class EvaluatorAbsOnly(EvaluatorBase):
    def __init__(self, leave_when_better_than=None):
        super().__init__(leave_when_better_than)
    def __call__(self, real_values, values):
        absolute = np.abs(abs_error(real_values, values))
        error = float(absolute.max())
        return self._get_ret(error)

class EvaluatorRelOnly(EvaluatorBase):
    def __init__(self, leave_when_better_than=None):
        super().__init__(leave_when_better_than)
    def __call__(self, real_values, values):
        relative = np.abs(rel_error(real_values, values))
        error = float(relative.max())
        return self._get_ret(error)

class EvaluatorMax(EvaluatorBase):
    def __init__(self, leave_when_better_than=None):
        super().__init__(leave_when_better_than)
    def __call__(self, real_values, values):
        absolute = np.abs(abs_error(real_values, values))
        relative = np.abs(rel_error(real_values, values))
        error = max(absolute.max(), relative.max())
        error = float(error)
        return self._get_ret(error)

class EvaluatorBalanced(EvaluatorBase):
    def __init__(self, leave_when_better_than=None, weight_abs=1.0,
            weight_rel=1.0):
        super().__init__(leave_when_better_than)
        self.weight_abs = weight_abs
        self.weight_rel = weight_rel

    def __call__(self, real_values, values):
        absolute = np.abs(abs_error(real_values, values))
        relative = np.abs(rel_error(real_values, values))
        max_abs = absolute.max()
        max_rel = relative.max()
        error = self.weight_abs * max_abs + self.weight_rel * max_rel
        error /= self.weight_abs + self.weight_rel
        error = float(error)
        return self._get_ret(error)



class Printer:
    def __init__(self, print_all_below=0.012, padto=40):
        self.padto = padto
        self.print_all_below = print_all_below
        self._best = float("inf")
        self._line_start = None

    def trying(self, pidxs, qidxs):
        s = f"{pidxs}, {qidxs}"
        print(s + " " * (self.padto - len(s)), end="\r")
        self._line_start = s

    def tried(self, ratpoly, error):
        if error <= max(self._best, self.print_all_below):
            s = f"{self._line_start} .."
            s += "." * (self.padto - len(s))
            s += f" {100 * error:.4g}%"
            print(s + " *" * (error <= self._best))
        self._best = min(self._best, error)
        self._line_start = None



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
        return np.sum(self.coeffs * ones[:, self.idxs], axis=-1)

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

    @classmethod
    def approximate(cls, real_values, *coords, idx_generator, evaluator,
            printer=None):

        ndim = len(coords)
        flatcoords = [x.ravel() for x in coords]
        real_values = real_values.ravel()

        if any((x == 0.0).any() for x in flatcoords):
            raise Exception("zero-coord in input")
        if any(((x > 0.0) != (x[0] > 0.0)).any() for x in flatcoords):
            raise Exception("zero-coord in input?")

        def initial_coeffs(pidxs, qidxs):
            initial = np.zeros(len(pidxs) - 1 + len(qidxs))
            initial[len(pidxs) - 1] = 1.0 # avoid /0
            return initial

        def ratpoly_from_coeffs(pidxs, qidxs, coeffs):
            pcoeffs = coeffs[:len(pidxs) - 1]
            pcoeffs = np.append(pcoeffs, 1.0)
            qcoeffs = coeffs[len(pidxs) - 1:]
            p = Polynomial(ndim, pidxs, pcoeffs, check=False)
            q = Polynomial(ndim, qidxs, qcoeffs, check=False)
            return RationalPolynomial(p, q)

        def find_best(pidxs, qidxs):
            maxidx = max(pidxs)
            maxidx = max(maxidx, max(qidxs))
            maxdegree = degreeof(ndim, maxidx)
            # dont evaluate super low degree, just bump up.
            maxdegree = max(maxdegree, 6 // ndim)
            if maxdegree > find_best.N:
                find_best.N = maxdegree
                find_best.ones = yup_all_ones(maxdegree, *flatcoords)

            def compute(coeffs, only_residuals=True):
                ratpoly = ratpoly_from_coeffs(pidxs, qidxs, coeffs)
                with np.errstate(divide="ignore"):
                    values = ratpoly.eval_ones(find_best.ones)
                if only_residuals:
                    return values - real_values
                else:
                    return ratpoly, *evaluator(real_values, values)
            # Optimise via least squares, since its significantly more stable
            # than a minimise-max-error optimiser.
            coeffs = initial_coeffs(pidxs, qidxs)
            res = scipy.optimize.least_squares(compute, coeffs, method="lm")
            return compute(res.x, False)
        find_best.N = -1
        find_best.ones = None

        best_ratpoly = None
        best_error = float("inf")

        for pidxs, qidxs in idx_generator:
            if printer is not None:
                printer.trying(pidxs, qidxs)
            ratpoly, error, leave = find_best(pidxs, qidxs)
            if printer is not None:
                printer.tried(ratpoly, error)

            if error <= best_error:
                best_ratpoly = ratpoly
                best_error = error
            if leave:
                break
        assert best_ratpoly is not None
        return best_ratpoly, best_error

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
        p = np.sum(self.p.coeffs * ones[:, self.p.idxs], axis=-1)
        q = np.sum(self.q.coeffs * ones[:, self.q.idxs], axis=-1)
        return p / q

    def abs_error(self, real_values, *coords):
        return abs_error(real_values, self(*coords))
    def rel_error(self, real_values, *coords):
        return rel_error(real_values, self(*coords))

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



class LookupTable:
    """
    Multi-dimensional lookup table, where each dimension is a table lookup of
    some count, however instead of a purely lerped grid it is pre-biased by a
    rational polynomial. This bias function should return in 0..1 for the
    intended input range, and this value will then be lerped across that table
    dimension (note the 0..1 is not enforced, and factors outside will be
    extrapolated).
    """

    class linear_lookup:
        def __init__(self, xlo, xhi):
            self.xlo = xlo
            self.xhi = xhi
            self.m = 1/(xhi - xlo)
            self.c = -xlo/(xhi - xlo)
    def __init__(self, values, *lookups):
        assert values.ndim >= 1
        assert values.size >= 1
        assert all(x >= 2 for x in values.shape)
        assert len(lookups) == values.ndim
        lookups = list(lookups)
        for i, lookup in enumerate(lookups):
            if isinstance(lookup, LookupTable.linear_lookup):
                lookups[i] = RationalPolynomial(
                    Polynomial.linear(lookup.m, lookup.c, values.ndim, i),
                    Polynomial.one(values.ndim),
                )
        assert all(isinstance(x, RationalPolynomial) for x in lookups)
        assert all(x.ndim == values.ndim for x in lookups)
        self.values = values
        self.lookups = lookups
    @property
    def size(self):
        return self.values.size
    @property
    def shape(self):
        return self.values.shape
    @property
    def ndim(self):
        return self.values.ndim

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
            # numpy indexing can suck my nuts
            N = self.shape[self.ndim - 1 - i]
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
            ret += weight * self.values[tuple(idx[::-1])]
        return ret
