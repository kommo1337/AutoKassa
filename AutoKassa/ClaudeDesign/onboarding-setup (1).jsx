import { useState, useEffect, useRef } from "react";

/* ====== PASSWORD STRENGTH ====== */
function getStrength(pw) {
  if (!pw) return { score: 0, label: "", color: "transparent" };
  let score = 0;
  if (pw.length >= 8) score++;
  if (pw.length >= 12) score++;
  if (/[A-Z]/.test(pw)) score++;
  if (/[0-9]/.test(pw)) score++;
  if (/[^A-Za-z0-9]/.test(pw)) score++;
  if (score <= 1) return { score: 1, label: "Слабый", color: "#ef4444" };
  if (score === 2) return { score: 2, label: "Средний", color: "#f59e0b" };
  if (score === 3) return { score: 3, label: "Хороший", color: "#22c55e" };
  return { score: 4, label: "Надёжный", color: "#16a34a" };
}

/* ====== EYE ICON ====== */
const EyeOn = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>
  </svg>
);
const EyeOff = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24"/>
    <line x1="1" y1="1" x2="23" y2="23"/>
  </svg>
);
const ChevDown = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="6 9 12 15 18 9"/>
  </svg>
);
const CheckIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12"/>
  </svg>
);
const ShieldIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
  </svg>
);
const BuildingIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
    <rect x="2" y="7" width="20" height="14" rx="2"/><path d="M16 21V5a2 2 0 00-2-2h-4a2 2 0 00-2 2v16"/>
  </svg>
);

const SECRET_QUESTIONS = [
  "Девичья фамилия матери",
  "Название первой школы",
  "Кличка первого питомца",
  "Любимый город детства",
  "Имя лучшего друга детства",
];

const STEPS = [
  { id: 0, label: "Компания" },
  { id: 1, label: "Безопасность" },
  { id: 2, label: "Готово" },
];

export default function OnboardingSetup() {
  const [step, setStep] = useState(0);
  const [mounted, setMounted] = useState(false);
  const [animating, setAnimating] = useState(false);

  // Step 0
  const [company, setCompany] = useState("");

  // Step 1
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [showCf, setShowCf] = useState(false);
  const [question, setQuestion] = useState(SECRET_QUESTIONS[0]);
  const [answer, setAnswer] = useState("");
  const [dropOpen, setDropOpen] = useState(false);
  const [errors, setErrors] = useState({});
  const dropRef = useRef(null);

  useEffect(() => {
    setTimeout(() => setMounted(true), 60);
  }, []);

  useEffect(() => {
    const handler = (e) => {
      if (dropRef.current && !dropRef.current.contains(e.target)) setDropOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const strength = getStrength(password);
  const pwMatch = confirm.length > 0 && password === confirm;
  const pwMismatch = confirm.length > 0 && password !== confirm;

  const goNext = () => {
    const errs = {};
    if (step === 0) {
      if (!company.trim()) errs.company = "Введите название";
    }
    if (step === 1) {
      if (!password) errs.password = "Введите пароль";
      else if (password.length < 6) errs.password = "Минимум 6 символов";
      if (!confirm) errs.confirm = "Повторите пароль";
      else if (password !== confirm) errs.confirm = "Пароли не совпадают";
      if (!answer.trim()) errs.answer = "Введите ответ";
    }
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});
    setAnimating(true);
    setTimeout(() => { setStep(s => s + 1); setAnimating(false); }, 280);
  };

  const goBack = () => {
    setAnimating(true);
    setTimeout(() => { setStep(s => s - 1); setAnimating(false); }, 280);
  };

  return (
    <div style={{
      display: "flex", height: "100vh", width: "100vw",
      fontFamily: "'DM Sans','Segoe UI',sans-serif",
      background: "#f0f2f5", color: "#1a1d23", overflow: "hidden",
    }}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=DM+Sans:opsz,wght@9..40,300;9..40,400;9..40,500;9..40,600;9..40,700&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        ::-webkit-scrollbar { width: 4px; }
        ::-webkit-scrollbar-thumb { background: #d0d5dd; border-radius: 3px; }
        @keyframes fadeSlideIn { from { opacity:0; transform:translateY(18px); } to { opacity:1; transform:translateY(0); } }
        @keyframes fadeSlideOut { from { opacity:1; transform:translateY(0); } to { opacity:0; transform:translateY(-14px); } }
        @keyframes panelIn { from { opacity:0; transform:translateX(-30px); } to { opacity:1; transform:translateX(0); } }
        @keyframes pulse { 0%,100% { opacity:.7; } 50% { opacity:1; } }
        @keyframes checkPop { 0% { transform:scale(0); opacity:0; } 70% { transform:scale(1.2); } 100% { transform:scale(1); opacity:1; } }
        @keyframes successIn { from { opacity:0; transform:scale(.92); } to { opacity:1; transform:scale(1); } }
        input:focus { outline: none; }
        button { font-family: inherit; }
        .input-field {
          width: 100%; padding: 11px 14px; border-radius: 10px;
          border: 1.5px solid #e8eaed; background: #fff;
          font-size: 14px; font-family: inherit; color: #1a1d23;
          transition: border-color .2s, box-shadow .2s;
        }
        .input-field:focus { border-color: #22c55e; box-shadow: 0 0 0 3px rgba(34,197,94,.1); }
        .input-field.error { border-color: #ef4444; box-shadow: 0 0 0 3px rgba(239,68,68,.08); }
        .input-field::placeholder { color: #b0b7c3; }
        .btn-primary {
          width: 100%; padding: 13px; border-radius: 12px; border: none;
          background: #22c55e; color: #fff; font-size: 15px; font-weight: 600;
          cursor: pointer; transition: all .2s; letter-spacing: .01em;
        }
        .btn-primary:hover { background: #16a34a; transform: translateY(-1px); box-shadow: 0 6px 20px rgba(34,197,94,.3); }
        .btn-primary:active { transform: translateY(0); }
        .btn-primary:disabled { background: #d1fae5; color: #6ee7b7; cursor: not-allowed; transform: none; box-shadow: none; }
        .btn-ghost {
          padding: 11px 20px; border-radius: 10px; border: 1.5px solid #e8eaed;
          background: transparent; color: #667085; font-size: 14px; font-weight: 500;
          cursor: pointer; transition: all .2s;
        }
        .btn-ghost:hover { border-color: #d0d5dd; background: #f9fafb; }
      `}</style>

      {/* ── LEFT PANEL ── */}
      <div style={{
        width: 420, flexShrink: 0,
        background: "linear-gradient(160deg,#1a1d23 0%,#24272e 100%)",
        display: "flex", flexDirection: "column",
        padding: "48px 44px",
        position: "relative", overflow: "hidden",
        animation: mounted ? "panelIn .5s cubic-bezier(.34,1.3,.64,1)" : "none",
      }}>
        {/* Decorative blobs */}
        <div style={{ position:"absolute", top:-80, right:-80, width:240, height:240, borderRadius:"50%", background:"radial-gradient(circle,rgba(34,197,94,.18) 0%,transparent 70%)", pointerEvents:"none" }} />
        <div style={{ position:"absolute", bottom:-60, left:-60, width:200, height:200, borderRadius:"50%", background:"radial-gradient(circle,rgba(99,102,241,.15) 0%,transparent 70%)", pointerEvents:"none" }} />

        {/* Logo */}
        <div style={{ display:"flex", alignItems:"center", gap:10, marginBottom:64 }}>
          <div style={{ width:38, height:38, borderRadius:10, background:"linear-gradient(135deg,#22c55e,#16a34a)", display:"flex", alignItems:"center", justifyContent:"center", fontSize:17, fontWeight:700, color:"#fff", boxShadow:"0 4px 14px rgba(34,197,94,.35)" }}>₽</div>
          <span style={{ fontSize:17, fontWeight:700, color:"#fff", letterSpacing:"-.01em" }}>АвтоКасса</span>
        </div>

        {/* Step progress */}
        <div style={{ marginBottom: 40 }}>
          {STEPS.map((s, i) => {
            const done = step > i;
            const active = step === i;
            return (
              <div key={s.id} style={{ display:"flex", alignItems:"flex-start", gap:14, marginBottom: i < STEPS.length - 1 ? 0 : 0 }}>
                <div style={{ display:"flex", flexDirection:"column", alignItems:"center" }}>
                  <div style={{
                    width: 32, height: 32, borderRadius: "50%", flexShrink: 0,
                    display: "flex", alignItems: "center", justifyContent: "center",
                    background: done ? "#22c55e" : active ? "rgba(34,197,94,.15)" : "rgba(255,255,255,.06)",
                    border: `2px solid ${done ? "#22c55e" : active ? "#22c55e" : "rgba(255,255,255,.12)"}`,
                    transition: "all .35s",
                    animation: done ? "checkPop .4s ease" : "none",
                  }}>
                    {done
                      ? <span style={{ color:"#fff" }}><CheckIcon /></span>
                      : <span style={{ fontSize:13, fontWeight:700, color: active ? "#22c55e" : "rgba(255,255,255,.35)" }}>{i + 1}</span>
                    }
                  </div>
                  {i < STEPS.length - 1 && (
                    <div style={{
                      width: 2, height: 36, marginTop: 4, marginBottom: 4,
                      background: done ? "#22c55e" : "rgba(255,255,255,.08)",
                      borderRadius: 2, transition: "background .4s",
                    }} />
                  )}
                </div>
                <div style={{ paddingTop: 6 }}>
                  <p style={{ fontSize:13, fontWeight:600, color: active ? "#fff" : done ? "rgba(255,255,255,.7)" : "rgba(255,255,255,.3)", transition:"color .3s" }}>{s.label}</p>
                  {active && (
                    <p style={{ fontSize:11, color:"rgba(255,255,255,.4)", marginTop:2, animation:"fadeSlideIn .3s ease" }}>
                      {i === 0 ? "Информация об организации" : i === 1 ? "Настройка безопасности" : "Всё готово"}
                    </p>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Bottom tagline */}
        <div style={{ marginTop:"auto" }}>
          <p style={{ fontSize:12, color:"rgba(255,255,255,.25)", lineHeight:1.6 }}>
            Ваши данные хранятся локально<br/>и защищены паролем
          </p>
        </div>
      </div>

      {/* ── RIGHT PANEL ── */}
      <div style={{
        flex:1, display:"flex", alignItems:"center", justifyContent:"center",
        padding: 40, overflowY:"auto",
      }}>
        <div style={{
          width: "100%", maxWidth: 420,
          animation: mounted ? (animating ? "fadeSlideOut .28s ease forwards" : "fadeSlideIn .38s cubic-bezier(.34,1.1,.64,1)") : "none",
        }}>

          {/* STEP 0 — Company */}
          {step === 0 && (
            <div>
              <div style={{ width:52, height:52, borderRadius:14, background:"linear-gradient(135deg,#f0fdf4,#dcfce7)", display:"flex", alignItems:"center", justifyContent:"center", marginBottom:24, color:"#16a34a", boxShadow:"0 2px 10px rgba(34,197,94,.12)" }}>
                <BuildingIcon />
              </div>
              <h1 style={{ fontSize:26, fontWeight:700, letterSpacing:"-.02em", marginBottom:6 }}>Добро пожаловать!</h1>
              <p style={{ fontSize:14, color:"#667085", marginBottom:36, lineHeight:1.55 }}>Давайте настроим АвтоКассу под ваш бизнес. Начнём с названия компании.</p>

              <div style={{ marginBottom:24 }}>
                <label style={{ display:"block", fontSize:13, fontWeight:600, color:"#374151", marginBottom:7 }}>Название компании / автосервиса</label>
                <input
                  className={`input-field${errors.company ? " error" : ""}`}
                  placeholder="Например: СТО «Мотор»"
                  value={company}
                  onChange={e => { setCompany(e.target.value); if (errors.company) setErrors({}); }}
                  autoFocus
                  onKeyDown={e => e.key === "Enter" && goNext()}
                />
                {errors.company && <p style={{ fontSize:12, color:"#ef4444", marginTop:5 }}>{errors.company}</p>}
              </div>

              <button className="btn-primary" onClick={goNext} disabled={!company.trim()}>
                Продолжить →
              </button>
            </div>
          )}

          {/* STEP 1 — Security */}
          {step === 1 && (
            <div>
              <div style={{ width:52, height:52, borderRadius:14, background:"linear-gradient(135deg,#f0fdf4,#dcfce7)", display:"flex", alignItems:"center", justifyContent:"center", marginBottom:24, color:"#16a34a", boxShadow:"0 2px 10px rgba(34,197,94,.12)" }}>
                <ShieldIcon />
              </div>
              <h1 style={{ fontSize:26, fontWeight:700, letterSpacing:"-.02em", marginBottom:6 }}>Защита приложения</h1>
              <p style={{ fontSize:14, color:"#667085", marginBottom:32, lineHeight:1.55 }}>Установите пароль и секретный вопрос для восстановления доступа.</p>

              {/* Password */}
              <div style={{ marginBottom:18 }}>
                <label style={{ display:"block", fontSize:13, fontWeight:600, color:"#374151", marginBottom:7 }}>Новый пароль</label>
                <div style={{ position:"relative" }}>
                  <input
                    className={`input-field${errors.password ? " error" : ""}`}
                    type={showPw ? "text" : "password"}
                    placeholder="Минимум 6 символов"
                    value={password}
                    style={{ paddingRight:42 }}
                    onChange={e => { setPassword(e.target.value); setErrors(p => ({ ...p, password: null })); }}
                    autoFocus
                  />
                  <button onClick={() => setShowPw(v => !v)} style={{ position:"absolute", right:13, top:"50%", transform:"translateY(-50%)", background:"none", border:"none", cursor:"pointer", color:"#98a2b3", padding:2, display:"flex", alignItems:"center" }}>
                    {showPw ? <EyeOff /> : <EyeOn />}
                  </button>
                </div>
                {errors.password && <p style={{ fontSize:12, color:"#ef4444", marginTop:5 }}>{errors.password}</p>}

                {/* Strength bar */}
                {password && (
                  <div style={{ marginTop:8, animation:"fadeSlideIn .2s ease" }}>
                    <div style={{ display:"flex", gap:4, marginBottom:5 }}>
                      {[1,2,3,4].map(i => (
                        <div key={i} style={{ flex:1, height:3, borderRadius:2, background: i <= strength.score ? strength.color : "#e8eaed", transition:"background .3s" }} />
                      ))}
                    </div>
                    <p style={{ fontSize:11, color: strength.color, fontWeight:600 }}>Надёжность: {strength.label}</p>
                  </div>
                )}
              </div>

              {/* Confirm */}
              <div style={{ marginBottom:22 }}>
                <label style={{ display:"block", fontSize:13, fontWeight:600, color:"#374151", marginBottom:7 }}>Подтверждение пароля</label>
                <div style={{ position:"relative" }}>
                  <input
                    className={`input-field${errors.confirm || pwMismatch ? " error" : ""}`}
                    type={showCf ? "text" : "password"}
                    placeholder="Повторите пароль"
                    value={confirm}
                    style={{ paddingRight: 42 }}
                    onChange={e => { setConfirm(e.target.value); setErrors(p => ({ ...p, confirm: null })); }}
                  />
                  <button onClick={() => setShowCf(v => !v)} style={{ position:"absolute", right:13, top:"50%", transform:"translateY(-50%)", background:"none", border:"none", cursor:"pointer", color:"#98a2b3", padding:2, display:"flex", alignItems:"center" }}>
                    {showCf ? <EyeOff /> : <EyeOn />}
                  </button>
                  {pwMatch && (
                    <div style={{ position:"absolute", right:42, top:"50%", transform:"translateY(-50%)", color:"#22c55e", display:"flex", animation:"checkPop .3s ease" }}>
                      <CheckIcon />
                    </div>
                  )}
                </div>
                {(errors.confirm || pwMismatch) && <p style={{ fontSize:12, color:"#ef4444", marginTop:5 }}>{errors.confirm || "Пароли не совпадают"}</p>}
              </div>

              {/* Divider */}
              <div style={{ height:1, background:"#f0f2f5", marginBottom:22 }} />

              {/* Secret question */}
              <div style={{ marginBottom:16 }}>
                <label style={{ display:"block", fontSize:13, fontWeight:600, color:"#374151", marginBottom:7 }}>Секретный вопрос</label>
                <div ref={dropRef} style={{ position:"relative" }}>
                  <button
                    onClick={() => setDropOpen(v => !v)}
                    style={{ width:"100%", padding:"11px 14px", borderRadius:10, border:`1.5px solid ${dropOpen ? "#22c55e" : "#e8eaed"}`, background:"#fff", display:"flex", alignItems:"center", justifyContent:"space-between", cursor:"pointer", fontSize:14, color:"#1a1d23", fontFamily:"inherit", transition:"border-color .2s, box-shadow .2s", boxShadow: dropOpen ? "0 0 0 3px rgba(34,197,94,.1)" : "none" }}
                  >
                    <span>{question}</span>
                    <span style={{ color:"#98a2b3", transition:"transform .2s", transform: dropOpen ? "rotate(180deg)" : "rotate(0deg)", display:"flex" }}><ChevDown /></span>
                  </button>
                  {dropOpen && (
                    <div style={{ position:"absolute", top:"calc(100% + 6px)", left:0, right:0, background:"#fff", border:"1.5px solid #e8eaed", borderRadius:12, boxShadow:"0 8px 30px rgba(0,0,0,.1)", zIndex:50, overflow:"hidden", animation:"fadeSlideIn .2s ease" }}>
                      {SECRET_QUESTIONS.map(q => (
                        <button key={q} onClick={() => { setQuestion(q); setDropOpen(false); }} style={{ width:"100%", padding:"11px 16px", border:"none", background: q === question ? "#f0fdf4" : "transparent", fontSize:13, color: q === question ? "#16a34a" : "#374151", fontFamily:"inherit", textAlign:"left", cursor:"pointer", display:"flex", alignItems:"center", justifyContent:"space-between", transition:"background .15s", fontWeight: q === question ? 600 : 400 }}
                          onMouseEnter={e => { if (q !== question) e.currentTarget.style.background = "#f9fafb"; }}
                          onMouseLeave={e => { if (q !== question) e.currentTarget.style.background = "transparent"; }}
                        >
                          {q}
                          {q === question && <span style={{ color:"#22c55e" }}><CheckIcon /></span>}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>

              <div style={{ marginBottom:28 }}>
                <label style={{ display:"block", fontSize:13, fontWeight:600, color:"#374151", marginBottom:7 }}>Ответ на вопрос</label>
                <input
                  className={`input-field${errors.answer ? " error" : ""}`}
                  placeholder="Введите ответ"
                  value={answer}
                  onChange={e => { setAnswer(e.target.value); setErrors(p => ({ ...p, answer: null })); }}
                />
                {errors.answer && <p style={{ fontSize:12, color:"#ef4444", marginTop:5 }}>{errors.answer}</p>}
              </div>

              <div style={{ display:"flex", gap:10 }}>
                <button className="btn-ghost" onClick={goBack}>← Назад</button>
                <button className="btn-primary" onClick={goNext} style={{ flex:1 }}>
                  Завершить настройку
                </button>
              </div>
            </div>
          )}

          {/* STEP 2 — Success */}
          {step === 2 && (
            <div style={{ textAlign:"center", animation:"successIn .45s cubic-bezier(.34,1.2,.64,1)" }}>
              <div style={{ width:80, height:80, borderRadius:"50%", background:"linear-gradient(135deg,#22c55e,#16a34a)", display:"flex", alignItems:"center", justifyContent:"center", margin:"0 auto 28px", boxShadow:"0 8px 30px rgba(34,197,94,.35)", animation:"successIn .5s cubic-bezier(.34,1.5,.64,1)" }}>
                <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
              </div>
              <h1 style={{ fontSize:26, fontWeight:700, letterSpacing:"-.02em", marginBottom:10 }}>Всё готово!</h1>
              <p style={{ fontSize:14, color:"#667085", marginBottom:8, lineHeight:1.6 }}>
                <strong style={{ color:"#1a1d23" }}>{company}</strong> успешно настроена.<br/>Ваше приложение защищено и готово к работе.
              </p>

              <div style={{ background:"#f9fafb", border:"1.5px solid #e8eaed", borderRadius:14, padding:"18px 20px", marginBottom:32, textAlign:"left" }}>
                {[
                  { label:"Компания", value: company },
                  { label:"Пароль", value: "•".repeat(password.length) },
                  { label:"Секретный вопрос", value: question },
                ].map(item => (
                  <div key={item.label} style={{ display:"flex", justifyContent:"space-between", alignItems:"center", padding:"7px 0", borderBottom:"1px solid #f0f2f5" }}>
                    <span style={{ fontSize:12, color:"#98a2b3", fontWeight:500 }}>{item.label}</span>
                    <span style={{ fontSize:13, color:"#374151", fontWeight:600, maxWidth:"60%", textAlign:"right", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }}>{item.value}</span>
                  </div>
                ))}
              </div>

              <button className="btn-primary" onClick={() => alert("Переход в приложение...")}>
                Войти в АвтоКассу →
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
