using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KellPersistence;
using System.Linq.Expressions;
using System.Collections.ObjectModel;

namespace Test
{
    [Serializable]
    public class MyData : ICustomObject
    {
        string str;

        public string Str
        {
            get { return str; }
            set { str = value; }
        }

        public MyData(string str)
        {
            this.str = str;
        }

        public MyData(Guid id, DataHeader header, string str)
        {
            this.id = id;
            this.header = header;
            this.str = str;
        }

        public object Clone()
        {
            return this.Clone();
        }

        Guid id;
        public Guid ID
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

        DataHeader header;
        public DataHeader Header
        {
            get
            {
                return header;
            }
            set
            {
                header = value;
            }
        }

        public override string ToString()
        {
            return "#" + this.Header + "#" + this.Str;
        }
    }
    /// <summary>
    /// 自定义数据的查询类
    /// 需要改进此类，暂时还未能实现自定义数据的查询
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CustomQueryProvider<T> : QueryProvider, IQueryable<T>, IQueryProvider
        where T : ICloneable
    {
        #region IQueryable Members

        Type IQueryable.ElementType
        {
            get { return typeof(MyData); }
        }

        Expression IQueryable.Expression
        {
            get { return Expression.Constant(this); }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return this; }
        }

        public override string GetQueryText(Expression expression)
        {
            return this.Execute(expression).ToString();
        }

        public override object Execute(Expression expression)
        {
            return this.Execute(expression);
        }

        #endregion

        #region IEnumerable<T> Members

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return (this as IQueryable).Provider.Execute<IEnumerator<T>>(_expression);
        }

        private Trunk<T> trunk;
        private IList<T> _data = new List<T>();
        private Expression _expression = null;

        public Trunk<T> Trunk
        {
            get { return trunk; }
            set { trunk = value; }
        }


        public Expression MyExpression
        {
            get { return _expression; }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (IEnumerator<T>)(this as IQueryable).GetEnumerator();
        }

        private void ProcessExpression(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Equal)
            {
                ProcessEqualResult((BinaryExpression)expression);
            }
            //if (expression.NodeType == ExpressionType.LessThan)
            //{
            //    _data = GetDatas();

            //    var query = from p in _data
            //                where p.Age < (int)GetValue((BinaryExpression)expression)
            //                select p;
            //    _data = query.ToList<T>();
            //}
            //if (expression.NodeType == ExpressionType.LessThanOrEqual)
            //{
            //    _data = GetDatas();

            //    var query = from p in _data
            //                where p.Age <= (int)GetValue((BinaryExpression)expression)
            //                select p;
            //    _data = query.ToList<T>();
            //}
            if (expression is UnaryExpression)
            {
                UnaryExpression uExp = expression as UnaryExpression;
                ProcessExpression(uExp.Operand);
            }
            else if (expression is LambdaExpression)
            {
                ProcessExpression(((LambdaExpression)expression).Body);
            }
            else if (expression is ParameterExpression)
            {
                if (((ParameterExpression)expression).Type == typeof(T))
                {
                    _data = GetDatas();
                }
            }
        }

        private void ProcessEqualResult(BinaryExpression expression)
        {
            if (expression.Right.NodeType == ExpressionType.Constant)
            {
                string name = (String)((ConstantExpression)expression.Right).Value;
                ProceesItem(name);
            }
        }

        private void ProceesItem(string name)
        {
            IList<T> filtered = new List<T>();

            foreach (T data in GetDatas())
            {
                MyData md = data as MyData;
                if (md != null)
                {
                    if (md.Str.Contains(name))
                    {
                        filtered.Add(data);
                    }
                }
            }
            _data = filtered;
        }

        private object GetValue(BinaryExpression expression)
        {
            if (expression.Right.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)expression.Right).Value;
            }
            return null;
        }

        IList<T> GetDatas()
        {
            List<T> datas = new List<T>();
            List<Data<T>> ds = trunk.SelectAll();
            foreach (Data<T> data in ds)
            {
                datas.Add(data.Buffer);
            }
            return datas;
        }

        #endregion

        #region IQueryProvider Members

        IQueryable<TResult> IQueryProvider.CreateQuery<TResult>(Expression expression)
        {
            if (typeof(TResult) != typeof(T))
                throw new Exception("Only " + typeof(T).FullName + " objects are supported.");

            this._expression = expression;

            AnalysisExpression(expression);
            return (IQueryable<TResult>)this;
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            AnalysisExpression(expression);
            return (IQueryable<T>)(this as IQueryProvider).CreateQuery<T>(expression);
        }

        TResult IQueryProvider.Execute<TResult>(Expression expression)
        {
            MethodCallExpression methodcall = _expression as MethodCallExpression;

            foreach (var param in methodcall.Arguments)
            {
                ProcessExpression(param);
            }
            return (TResult)_data.GetEnumerator();
        }

        object IQueryProvider.Execute(Expression expression)
        {
            return (this as IQueryProvider).Execute<IEnumerator<T>>(expression);
        }

        #endregion

        public string AnalysisExpression(Expression exp)
        {
            StringBuilder s = new StringBuilder();
            
            switch (exp.NodeType)
            {
                case ExpressionType.Call:
                    {
                        MethodCallExpression mce = exp as MethodCallExpression;
                        Console.WriteLine("The Method Is {0}", mce.Method.Name);
                        s.Append(mce.Method.Name + " ");
                        for (int i = 0; i < mce.Arguments.Count; i++)
                        {
                            s.Append(AnalysisExpression(mce.Arguments[i]));
                        }
                    }
                    break;
                case ExpressionType.Quote:
                    {
                        UnaryExpression ue = exp as UnaryExpression;
                        s.Append(AnalysisExpression(ue.Operand));
                    }
                    break;
                case ExpressionType.Lambda:
                    {
                        LambdaExpression le = exp as LambdaExpression;
                        s.Append(AnalysisExpression(le.Body));
                    }
                    break;
                case ExpressionType.Equal:
                    {
                        BinaryExpression be = exp as BinaryExpression;
                        Console.WriteLine("The Method Is {0}", exp.NodeType.ToString());
                        s.Append(AnalysisExpression(be.Left));
                        s.Append(exp.NodeType.ToString() + " ");
                        s.Append(AnalysisExpression(be.Right));
                    }
                    break;
                case ExpressionType.Constant:
                    {
                        ConstantExpression ce = exp as ConstantExpression;
                        Console.WriteLine("The Value Type Is {0}", ce.Value.ToString());
                        s.Append(ce.Value.ToString() + " ");
                    }
                    break;
                case ExpressionType.Parameter:
                    {
                        ParameterExpression pe = exp as ParameterExpression;
                        Console.WriteLine("The Parameter Is {0}", pe.Name);
                        s.Append(pe.Name + " ");
                    }
                    break;
                default:
                    {
                        Console.Write("UnKnown");
                    }
                    break;
            }
            return s.ToString();
        }
    }


    /*
    public class DataProviderContext
    {
        internal static object Execute(Expression expression, bool IsEnumerable, Trunk<MyData> trunk)
        {
            // The expression must represent a query over the data source.
            if (!IsQueryOverDataSource(expression))
                throw new InvalidProgramException("No query over the data source was specified.");

            // Find the call to Where() and get the lambda expression predicate.
            InnermostWhereFinder whereFinder = new InnermostWhereFinder();
            MethodCallExpression whereExpression = whereFinder.GetInnermostWhere(expression);
            LambdaExpression lambdaExpression = (LambdaExpression)((UnaryExpression)(whereExpression.Arguments[1])).Operand;

            // Send the lambda expression through the partial evaluator.
            lambdaExpression = (LambdaExpression)Evaluator.PartialEval(lambdaExpression);

            // Get the place name(s) to query the Web service with.
            LocationFinder lf = new LocationFinder(lambdaExpression.Body);
            List<string> locations = lf.Locations;
            if (locations.Count == 0)
                throw new InvalidQueryException("You must specify at least one place name in your query.");

            // Call the Web service and get the results.
            IList<MyData> datas = GetDatas(trunk);

            // Copy the IEnumerable datas to an IQueryable.
            IQueryable<MyData> queryableMyDatas = datas.AsQueryable<MyData>();

            // Copy the expression tree that was passed in, changing only the first
            // argument of the innermost MethodCallExpression.
            ExpressionTreeModifier treeCopier = new ExpressionTreeModifier(queryableMyDatas);
            Expression newExpressionTree = treeCopier.CopyAndModify(expression);

            // This step creates an IQueryable that executes by replacing Queryable methods with Enumerable methods.
            if (IsEnumerable)
                return queryableMyDatas.Provider.CreateQuery(newExpressionTree);
            else
                return queryableMyDatas.Provider.Execute(newExpressionTree);
        }

        private static bool IsQueryOverDataSource(Expression expression)
        {
            // If expression represents an unqueried IQueryable data source instance,
            // expression is of type ConstantExpression, not MethodCallExpression.
            return (expression is MethodCallExpression);
        }


        private static IList<MyData> GetDatas(Trunk<MyData> trunk)
        {
            List<MyData> datas = new List<MyData>();
            List<Data<MyData>> ds = trunk.SelectAll();
            foreach (Data<MyData> data in ds)
            {
                datas.Add(data.Buffer);
            }
            return datas;
        }
    }

    internal class InnermostWhereFinder : ExpressionVisitor
    {
        private MethodCallExpression innermostWhereExpression;

        public MethodCallExpression GetInnermostWhere(Expression expression)
        {
            Visit(expression);
            return innermostWhereExpression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Method.Name == "Where")
                innermostWhereExpression = expression;

            Visit(expression.Arguments[0]);

            return expression;
        }
    }

    internal class LocationFinder : ExpressionVisitor
    {
        private Expression expression;
        private List<string> locations;

        public LocationFinder(Expression exp)
        {
            this.expression = exp;
        }

        public List<string> Locations
        {
            get
            {
                if (locations == null)
                {
                    locations = new List<string>();
                    this.Visit(this.expression);
                }
                return this.locations;
            }
        }

        protected override Expression VisitBinary(BinaryExpression be)
        {
            if (be.NodeType == ExpressionType.Equal)
            {
                if (ExpressionTreeHelpers.IsMemberEqualsValueExpression(be, typeof(MyData), "Str"))
                {
                    locations.Add(ExpressionTreeHelpers.GetValueFromEqualsExpression(be, typeof(MyData), "Str"));
                    return be;
                }
                else
                    return base.VisitBinary(be);
            }
            else
                return base.VisitBinary(be);
        }
    }

    internal class ExpressionTreeModifier : ExpressionVisitor
    {
        private IQueryable<MyData> queryableMyDatas;

        internal ExpressionTreeModifier(IQueryable<MyData> datas)
        {
            this.queryableMyDatas = datas;
        }

        internal Expression CopyAndModify(Expression expression)
        {
            return this.Visit(expression);
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            // Replace the constant QueryableTerraServerData arg with the queryable MyData collection.
            if (c.Type == typeof(Data<MyData>))
                return Expression.Constant(this.queryableMyDatas);
            else
                return c;
        }
    }

    public static class Evaluator
    {
        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <param name="fnCanBeEvaluated">A function that decides whether a given expression node can be part of the local function.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            return new SubtreeEvaluator(new Nominator(fnCanBeEvaluated).Nominate(expression)).Eval(expression);
        }

        /// <summary>
        /// Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression PartialEval(Expression expression)
        {
            return PartialEval(expression, Evaluator.CanBeEvaluatedLocally);
        }

        private static bool CanBeEvaluatedLocally(Expression expression)
        {
            return expression.NodeType != ExpressionType.Parameter;
        }

        /// <summary>
        /// Evaluates & redatas sub-trees when first candidate is reached (top-down)
        /// </summary>
        class SubtreeEvaluator : ExpressionVisitor
        {
            HashSet<Expression> candidates;

            internal SubtreeEvaluator(HashSet<Expression> candidates)
            {
                this.candidates = candidates;
            }

            internal Expression Eval(Expression exp)
            {
                return this.Visit(exp);
            }

            protected override Expression Visit(Expression exp)
            {
                if (exp == null)
                {
                    return null;
                }
                if (this.candidates.Contains(exp))
                {
                    return this.Evaluate(exp);
                }
                return base.Visit(exp);
            }

            private Expression Evaluate(Expression e)
            {
                if (e.NodeType == ExpressionType.Constant)
                {
                    return e;
                }
                LambdaExpression lambda = Expression.Lambda(e);
                Delegate fn = lambda.Compile();
                return Expression.Constant(fn.DynamicInvoke(null), e.Type);
            }
        }

        /// <summary>
        /// Performs bottom-up analysis to determine which nodes can possibly
        /// be part of an evaluated sub-tree.
        /// </summary>
        class Nominator : ExpressionVisitor
        {
            Func<Expression, bool> fnCanBeEvaluated;
            HashSet<Expression> candidates;
            bool cannotBeEvaluated;

            internal Nominator(Func<Expression, bool> fnCanBeEvaluated)
            {
                this.fnCanBeEvaluated = fnCanBeEvaluated;
            }

            internal HashSet<Expression> Nominate(Expression expression)
            {
                this.candidates = new HashSet<Expression>();
                this.Visit(expression);
                return this.candidates;
            }

            protected override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    bool saveCannotBeEvaluated = this.cannotBeEvaluated;
                    this.cannotBeEvaluated = false;
                    base.Visit(expression);
                    if (!this.cannotBeEvaluated)
                    {
                        if (this.fnCanBeEvaluated(expression))
                        {
                            this.candidates.Add(expression);
                        }
                        else
                        {
                            this.cannotBeEvaluated = true;
                        }
                    }
                    this.cannotBeEvaluated |= saveCannotBeEvaluated;
                }
                return expression;
            }
        }
    }

    internal class ExpressionTreeHelpers
    {
        internal static bool IsMemberEqualsValueExpression(Expression exp, Type declaringType, string memberName)
        {
            if (exp.NodeType != ExpressionType.Equal)
                return false;

            BinaryExpression be = (BinaryExpression)exp;

            // Assert.
            if (ExpressionTreeHelpers.IsSpecificMemberExpression(be.Left, declaringType, memberName) &&
                ExpressionTreeHelpers.IsSpecificMemberExpression(be.Right, declaringType, memberName))
                throw new Exception("Cannot have 'member' == 'member' in an expression!");

            return (ExpressionTreeHelpers.IsSpecificMemberExpression(be.Left, declaringType, memberName) ||
                ExpressionTreeHelpers.IsSpecificMemberExpression(be.Right, declaringType, memberName));
        }

        internal static bool IsSpecificMemberExpression(Expression exp, Type declaringType, string memberName)
        {
            return ((exp is MemberExpression) &&
                (((MemberExpression)exp).Member.DeclaringType == declaringType) &&
                (((MemberExpression)exp).Member.Name == memberName));
        }

        internal static string GetValueFromEqualsExpression(BinaryExpression be, Type memberDeclaringType, string memberName)
        {
            if (be.NodeType != ExpressionType.Equal)
                throw new Exception("There is a bug in this program.");

            if (be.Left.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression me = (MemberExpression)be.Left;

                if (me.Member.DeclaringType == memberDeclaringType && me.Member.Name == memberName)
                {
                    return GetValueFromExpression(be.Right);
                }
            }
            else if (be.Right.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression me = (MemberExpression)be.Right;

                if (me.Member.DeclaringType == memberDeclaringType && me.Member.Name == memberName)
                {
                    return GetValueFromExpression(be.Left);
                }
            }

            // We should have returned by now.
            throw new Exception("There is a bug in this program.");
        }

        internal static string GetValueFromExpression(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Constant)
                return (string)(((ConstantExpression)expression).Value);
            else
                throw new InvalidQueryException(
                    String.Format("The expression type {0} is not supported to obtain a value.", expression.NodeType));
        }
    }

    public abstract class ExpressionVisitor
    {
        protected ExpressionVisitor()
        {
        }

        protected virtual Expression Visit(Expression exp)
        {
            if (exp == null)
                return exp;
            switch (exp.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return this.VisitUnary((UnaryExpression)exp);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                    return this.VisitBinary((BinaryExpression)exp);
                case ExpressionType.TypeIs:
                    return this.VisitTypeIs((TypeBinaryExpression)exp);
                case ExpressionType.Conditional:
                    return this.VisitConditional((ConditionalExpression)exp);
                case ExpressionType.Constant:
                    return this.VisitConstant((ConstantExpression)exp);
                case ExpressionType.Parameter:
                    return this.VisitParameter((ParameterExpression)exp);
                case ExpressionType.MemberAccess:
                    return this.VisitMemberAccess((MemberExpression)exp);
                case ExpressionType.Call:
                    return this.VisitMethodCall((MethodCallExpression)exp);
                case ExpressionType.Lambda:
                    return this.VisitLambda((LambdaExpression)exp);
                case ExpressionType.New:
                    return this.VisitNew((NewExpression)exp);
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return this.VisitNewArray((NewArrayExpression)exp);
                case ExpressionType.Invoke:
                    return this.VisitInvocation((InvocationExpression)exp);
                case ExpressionType.MemberInit:
                    return this.VisitMemberInit((MemberInitExpression)exp);
                case ExpressionType.ListInit:
                    return this.VisitListInit((ListInitExpression)exp);
                default:
                    throw new Exception(string.Format("Unhandled expression type: '{0}'", exp.NodeType));
            }
        }

        protected virtual MemberBinding VisitBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return this.VisitMemberAssignment((MemberAssignment)binding);
                case MemberBindingType.MemberBinding:
                    return this.VisitMemberMemberBinding((MemberMemberBinding)binding);
                case MemberBindingType.ListBinding:
                    return this.VisitMemberListBinding((MemberListBinding)binding);
                default:
                    throw new Exception(string.Format("Unhandled binding type '{0}'", binding.BindingType));
            }
        }

        protected virtual ElementInit VisitElementInitializer(ElementInit initializer)
        {
            ReadOnlyCollection<Expression> arguments = this.VisitExpressionList(initializer.Arguments);
            if (arguments != initializer.Arguments)
            {
                return Expression.ElementInit(initializer.AddMethod, arguments);
            }
            return initializer;
        }

        protected virtual Expression VisitUnary(UnaryExpression u)
        {
            Expression operand = this.Visit(u.Operand);
            if (operand != u.Operand)
            {
                return Expression.MakeUnary(u.NodeType, operand, u.Type, u.Method);
            }
            return u;
        }

        protected virtual Expression VisitBinary(BinaryExpression b)
        {
            Expression left = this.Visit(b.Left);
            Expression right = this.Visit(b.Right);
            Expression conversion = this.Visit(b.Conversion);
            if (left != b.Left || right != b.Right || conversion != b.Conversion)
            {
                if (b.NodeType == ExpressionType.Coalesce && b.Conversion != null)
                    return Expression.Coalesce(left, right, conversion as LambdaExpression);
                else
                    return Expression.MakeBinary(b.NodeType, left, right, b.IsLiftedToNull, b.Method);
            }
            return b;
        }

        protected virtual Expression VisitTypeIs(TypeBinaryExpression b)
        {
            Expression expr = this.Visit(b.Expression);
            if (expr != b.Expression)
            {
                return Expression.TypeIs(expr, b.TypeOperand);
            }
            return b;
        }

        protected virtual Expression VisitConstant(ConstantExpression c)
        {
            return c;
        }

        protected virtual Expression VisitConditional(ConditionalExpression c)
        {
            Expression test = this.Visit(c.Test);
            Expression ifTrue = this.Visit(c.IfTrue);
            Expression ifFalse = this.Visit(c.IfFalse);
            if (test != c.Test || ifTrue != c.IfTrue || ifFalse != c.IfFalse)
            {
                return Expression.Condition(test, ifTrue, ifFalse);
            }
            return c;
        }

        protected virtual Expression VisitParameter(ParameterExpression p)
        {
            return p;
        }

        protected virtual Expression VisitMemberAccess(MemberExpression m)
        {
            Expression exp = this.Visit(m.Expression);
            if (exp != m.Expression)
            {
                return Expression.MakeMemberAccess(exp, m.Member);
            }
            return m;
        }

        protected virtual Expression VisitMethodCall(MethodCallExpression m)
        {
            Expression obj = this.Visit(m.Object);
            IEnumerable<Expression> args = this.VisitExpressionList(m.Arguments);
            if (obj != m.Object || args != m.Arguments)
            {
                return Expression.Call(obj, m.Method, args);
            }
            return m;
        }

        protected virtual ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
        {
            List<Expression> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                Expression p = this.Visit(original[i]);
                if (list != null)
                {
                    list.Add(p);
                }
                else if (p != original[i])
                {
                    list = new List<Expression>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(p);
                }
            }
            if (list != null)
            {
                return list.AsReadOnly();
            }
            return original;
        }

        protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
        {
            Expression e = this.Visit(assignment.Expression);
            if (e != assignment.Expression)
            {
                return Expression.Bind(assignment.Member, e);
            }
            return assignment;
        }

        protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
        {
            IEnumerable<MemberBinding> bindings = this.VisitBindingList(binding.Bindings);
            if (bindings != binding.Bindings)
            {
                return Expression.MemberBind(binding.Member, bindings);
            }
            return binding;
        }

        protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
        {
            IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(binding.Initializers);
            if (initializers != binding.Initializers)
            {
                return Expression.ListBind(binding.Member, initializers);
            }
            return binding;
        }

        protected virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
        {
            List<MemberBinding> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                MemberBinding b = this.VisitBinding(original[i]);
                if (list != null)
                {
                    list.Add(b);
                }
                else if (b != original[i])
                {
                    list = new List<MemberBinding>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(b);
                }
            }
            if (list != null)
                return list;
            return original;
        }

        protected virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
        {
            List<ElementInit> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                ElementInit init = this.VisitElementInitializer(original[i]);
                if (list != null)
                {
                    list.Add(init);
                }
                else if (init != original[i])
                {
                    list = new List<ElementInit>(n);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(original[j]);
                    }
                    list.Add(init);
                }
            }
            if (list != null)
                return list;
            return original;
        }

        protected virtual Expression VisitLambda(LambdaExpression lambda)
        {
            Expression body = this.Visit(lambda.Body);
            if (body != lambda.Body)
            {
                return Expression.Lambda(lambda.Type, body, lambda.Parameters);
            }
            return lambda;
        }

        protected virtual NewExpression VisitNew(NewExpression nex)
        {
            IEnumerable<Expression> args = this.VisitExpressionList(nex.Arguments);
            if (args != nex.Arguments)
            {
                if (nex.Members != null)
                    return Expression.New(nex.Constructor, args, nex.Members);
                else
                    return Expression.New(nex.Constructor, args);
            }
            return nex;
        }

        protected virtual Expression VisitMemberInit(MemberInitExpression init)
        {
            NewExpression n = this.VisitNew(init.NewExpression);
            IEnumerable<MemberBinding> bindings = this.VisitBindingList(init.Bindings);
            if (n != init.NewExpression || bindings != init.Bindings)
            {
                return Expression.MemberInit(n, bindings);
            }
            return init;
        }

        protected virtual Expression VisitListInit(ListInitExpression init)
        {
            NewExpression n = this.VisitNew(init.NewExpression);
            IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(init.Initializers);
            if (n != init.NewExpression || initializers != init.Initializers)
            {
                return Expression.ListInit(n, initializers);
            }
            return init;
        }

        protected virtual Expression VisitNewArray(NewArrayExpression na)
        {
            IEnumerable<Expression> exprs = this.VisitExpressionList(na.Expressions);
            if (exprs != na.Expressions)
            {
                if (na.NodeType == ExpressionType.NewArrayInit)
                {
                    return Expression.NewArrayInit(na.Type.GetElementType(), exprs);
                }
                else
                {
                    return Expression.NewArrayBounds(na.Type.GetElementType(), exprs);
                }
            }
            return na;
        }

        protected virtual Expression VisitInvocation(InvocationExpression iv)
        {
            IEnumerable<Expression> args = this.VisitExpressionList(iv.Arguments);
            Expression expr = this.Visit(iv.Expression);
            if (args != iv.Arguments || expr != iv.Expression)
            {
                return Expression.Invoke(expr, args);
            }
            return iv;
        }
    }

    class InvalidQueryException : System.Exception
    {
        private string message;

        public InvalidQueryException(string message)
        {
            this.message = message + " ";
        }

        public override string Message
        {
            get
            {
                return "The client query is invalid: " + message;
            }
        }
    }
    */
}
