import { useState, useRef, useEffect, useCallback } from "react";

/* ====== HELPERS ====== */
const fm = n => { const a=Math.abs(n); return (n>0?"+":n<0?"−":"")+a.toLocaleString("ru-RU"); };
const fmtD = d => `${String(d.getDate()).padStart(2,"0")}.${String(d.getMonth()+1).padStart(2,"0")}.${d.getFullYear()}`;
const today = new Date();
const todayStr = fmtD(today);

/* ====== DATA ====== */
const initialTx = [
  {id:1,date:"21.02.2026",type:"expense",cat:"Запчасти",amount:-18200,wallet:"cash"},
  {id:2,date:"21.02.2026",type:"income",cat:"Оплата за ТО",amount:45000,wallet:"card"},
  {id:3,date:"21.02.2026",type:"expense",cat:"Расходники",amount:-3400,wallet:"cash"},
  {id:4,date:"20.02.2026",type:"expense",cat:"Аванс",amount:-185000,wallet:"cash"},
  {id:5,date:"20.02.2026",type:"income",cat:"Бюджет начальный",amount:250000,wallet:"cash"},
  {id:6,date:"20.02.2026",type:"income",cat:"Бюджет начальный",amount:744,wallet:"card"},
  {id:7,date:"20.02.2026",type:"expense",cat:"Аванс",amount:-14,wallet:"cash"},
  {id:8,date:"19.02.2026",type:"expense",cat:"Аванс",amount:-100000,wallet:"card"},
  {id:9,date:"19.02.2026",type:"expense",cat:"Зарплата",amount:-55000,wallet:"cash"},
  {id:10,date:"19.02.2026",type:"income",cat:"Оплата за ТО",amount:32000,wallet:"card"},
  {id:11,date:"18.02.2026",type:"expense",cat:"Аренда",amount:-34888,wallet:"cash"},
  {id:12,date:"18.02.2026",type:"expense",cat:"Запчасти",amount:-12500,wallet:"cash"},
  {id:13,date:"18.02.2026",type:"income",cat:"Оплата за ТО",amount:45000,wallet:"card"},
  {id:14,date:"17.02.2026",type:"expense",cat:"Коммунальные",amount:-8700,wallet:"card"},
  {id:15,date:"17.02.2026",type:"expense",cat:"Расходники",amount:-4200,wallet:"cash"},
];

const PALETTE=["#6366f1","#f59e0b","#ec4899","#8b5cf6","#14b8a6","#f97316","#06b6d4","#84cc16","#ef4444","#64748b","#3b82f6","#94a3b8"];
const initCats=[
  {id:1,name:"Аванс",color:"#6366f1"},{id:2,name:"Запчасти",color:"#f59e0b"},{id:3,name:"Зарплата",color:"#ec4899"},{id:4,name:"Аренда",color:"#8b5cf6"},
  {id:5,name:"Инструменты",color:"#14b8a6"},{id:6,name:"Расходники",color:"#f97316"},{id:7,name:"Коммунальные",color:"#06b6d4"},{id:8,name:"Реклама",color:"#84cc16"},
  {id:9,name:"Налоги",color:"#ef4444"},{id:10,name:"Страховка",color:"#64748b"},{id:11,name:"Транспорт",color:"#3b82f6"},{id:12,name:"Другое",color:"#94a3b8"},
];
const extraCatColors={"Оплата за ТО":"#22c55e","Бюджет начальный":"#10b981"};
const navItems = [{icon:"home",label:"Главная",active:true},{icon:"list",label:"Операции"},{icon:"chart",label:"Отчёты"},{icon:"settings",label:"Настройки"}];

/* ====== ICONS ====== */
const IC={
  home:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>,
  list:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>,
  chart:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>,
  tag:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20.59 13.41l-7.17 7.17a2 2 0 01-2.83 0L2 12V2h10l8.59 8.59a2 2 0 010 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/></svg>,
  settings:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg>,
  plus:<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>,
  lock:<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0110 0v4"/></svg>,
  arrowUp:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></svg>,
  arrowDown:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>,
  info:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>,
  chevron:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>,
  chevDown:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9"/></svg>,
  edit:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>,
  trash:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>,
  copy:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>,
  cal:<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>,
  calc:<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/></svg>,
  chevL:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>,
  chevR:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>,
  close:<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
  bksp:<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 4H8l-7 8 7 8h13a2 2 0 002-2V6a2 2 0 00-2-2z"/><line x1="18" y1="9" x2="12" y2="15"/><line x1="12" y1="9" x2="18" y2="15"/></svg>,
  check:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>,
  undo:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 102.13-9.36L1 10"/></svg>,
  cash:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2"/><circle cx="12" cy="12" r="4"/></svg>,
  card:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>,
  scale:<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 3h5v5"/><path d="M8 3H3v5"/><path d="M12 22V8"/><path d="M20 7l-8 5-8-5"/></svg>,
  sun:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>,
  gear:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg>,
  plusSm:<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>,
};

/* ====== SMALL COMPONENTS ====== */
function Tip({children,text}){const[s,setS]=useState(false);const[p,setP]=useState({t:0,l:0});const r=useRef(null);return(<div ref={r} onMouseEnter={()=>{if(r.current){const b=r.current.getBoundingClientRect();setP({t:b.top+b.height/2,l:b.right+10});}setS(true);}} onMouseLeave={()=>setS(false)} style={{position:"relative"}}>{children}{s&&<div style={{position:"fixed",top:p.t,left:p.l,transform:"translateY(-50%)",background:"#1a1d23",color:"#fff",fontSize:11,fontWeight:500,padding:"5px 10px",borderRadius:7,whiteSpace:"nowrap",zIndex:999,pointerEvents:"none",boxShadow:"0 4px 12px rgba(0,0,0,.2)",animation:"tipIn .15s ease"}}>{text}</div>}</div>);}

let tid=0;
function Toasts({toasts,onDismiss,onUndo}){return(<div style={{position:"fixed",bottom:24,left:"50%",transform:"translateX(-50%)",zIndex:300,display:"flex",flexDirection:"column-reverse",gap:8,pointerEvents:"none"}}>{toasts.map(t=><TOne key={t.id} t={t} onD={onDismiss} onU={onUndo}/>)}</div>);}
function TOne({t,onD,onU}){const[pr,setPr]=useState(100);const[out,setOut]=useState(false);const iv=useRef(null);
  useEffect(()=>{iv.current=setInterval(()=>{setPr(p=>{const n=p-.6;if(n<=0){clearInterval(iv.current);setOut(true);setTimeout(()=>onD(t.id),350);return 0;}return n;});},30);return()=>clearInterval(iv.current);},[t.id,onD]);
  const doU=()=>{clearInterval(iv.current);setOut(true);onU(t);setTimeout(()=>onD(t.id),350);};
  const cl={delete:{bg:"#fef2f2",bd:"#fecaca",br:"#ef4444"},edit:{bg:"#eff6ff",bd:"#bfdbfe",br:"#3b82f6"},add:{bg:"#f0fdf4",bd:"#bbf7d0",br:"#22c55e"}};const c=cl[t.action]||cl.add;
  return(<div style={{background:c.bg,border:`1px solid ${c.bd}`,borderRadius:14,padding:"12px 16px",minWidth:340,maxWidth:420,pointerEvents:"auto",boxShadow:"0 12px 40px rgba(0,0,0,.1)",animation:out?"toastOut .35s ease forwards":"toastIn .4s cubic-bezier(.34,1.56,.64,1)",position:"relative",overflow:"hidden"}}>
    <div style={{position:"absolute",bottom:0,left:0,height:3,background:c.br,width:`${pr}%`,transition:"width 30ms linear",opacity:.5}}/>
    <div style={{display:"flex",alignItems:"center",gap:10}}>
      <div style={{width:28,height:28,borderRadius:8,background:"#fff",display:"flex",alignItems:"center",justifyContent:"center",color:c.br,flexShrink:0,boxShadow:"0 1px 3px rgba(0,0,0,.06)"}}>{t.action==="delete"?IC.trash:t.action==="edit"?IC.edit:IC.check}</div>
      <div style={{flex:1,minWidth:0}}><p style={{fontSize:13,fontWeight:600,color:"#1a1d23"}}>{t.title}</p><p style={{fontSize:11,color:"#667085",overflow:"hidden",textOverflow:"ellipsis",whiteSpace:"nowrap"}}>{t.message}</p></div>
      {t.undoData&&<button onClick={doU} style={{display:"flex",alignItems:"center",gap:4,padding:"6px 12px",borderRadius:7,border:"none",background:"#fff",fontSize:11,fontWeight:600,color:c.br,cursor:"pointer",flexShrink:0,boxShadow:"0 1px 3px rgba(0,0,0,.08)",transition:"all .15s"}} onMouseEnter={e=>e.currentTarget.style.transform="scale(1.04)"} onMouseLeave={e=>e.currentTarget.style.transform="scale(1)"}><span style={{display:"flex"}}>{IC.undo}</span>Отменить</button>}
    </div></div>);}

function CtxMenu({x,y,onEdit,onDup,onDel,onClose}){const r=useRef(null);
  useEffect(()=>{const h=e=>{if(r.current&&!r.current.contains(e.target))onClose();};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[onClose]);
  const mi=(ic,lb,cl,fn)=>(<button key={lb} onClick={()=>{fn();onClose();}} style={{display:"flex",alignItems:"center",gap:9,width:"100%",border:"none",background:"none",padding:"8px 12px",fontSize:12,fontWeight:450,color:cl||"#344054",cursor:"pointer",borderRadius:6,fontFamily:"inherit",transition:"background .15s"}} onMouseEnter={e=>e.currentTarget.style.background="#f8f9fb"} onMouseLeave={e=>e.currentTarget.style.background="none"}><span style={{display:"flex",color:cl||"#667085"}}>{ic}</span>{lb}</button>);
  return(<div ref={r} style={{position:"fixed",top:y,left:x,background:"#fff",borderRadius:11,border:"1px solid #e8eaed",padding:4,minWidth:170,zIndex:200,boxShadow:"0 12px 40px rgba(0,0,0,.15)",animation:"scaleIn .12s cubic-bezier(.34,1.56,.64,1)"}}>{mi(IC.edit,"Редактировать",null,onEdit)}{mi(IC.copy,"Дублировать",null,onDup)}<div style={{height:1,background:"#f0f2f5",margin:"3px 0"}}/>{mi(IC.trash,"Удалить","#ef4444",onDel)}</div>);}

function CalPick({sel,onSel,onClose}){const[vm,setVm]=useState(sel.getMonth());const[vy,setVy]=useState(sel.getFullYear());const r=useRef(null);
  useEffect(()=>{const h=e=>{if(r.current&&!r.current.contains(e.target))onClose();};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[onClose]);
  const mo=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];const wd=["Пн","Вт","Ср","Чт","Пт","Сб","Вс"];
  let sd=new Date(vy,vm,1).getDay()-1;if(sd<0)sd=6;const dim=new Date(vy,vm+1,0).getDate();const cells=[];for(let i=0;i<sd;i++)cells.push(null);for(let d=1;d<=dim;d++)cells.push(d);
  const isSel=d=>d===sel.getDate()&&vm===sel.getMonth()&&vy===sel.getFullYear();const isNow=d=>{const t=new Date();return d===t.getDate()&&vm===t.getMonth()&&vy===t.getFullYear();};
  return(<div ref={r} style={{position:"absolute",bottom:"100%",left:0,marginBottom:6,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:14,zIndex:10,width:275,boxShadow:"0 12px 40px rgba(0,0,0,.12)",animation:"scaleIn .2s cubic-bezier(.34,1.56,.64,1)"}}>
    <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:10}}><button onClick={()=>{if(vm===0){setVm(11);setVy(y=>y-1);}else setVm(m=>m-1);}} style={{border:"none",background:"none",cursor:"pointer",padding:3,display:"flex",color:"#667085"}}>{IC.chevL}</button><span style={{fontSize:13,fontWeight:600}}>{mo[vm]} {vy}</span><button onClick={()=>{if(vm===11){setVm(0);setVy(y=>y+1);}else setVm(m=>m+1);}} style={{border:"none",background:"none",cursor:"pointer",padding:3,display:"flex",color:"#667085"}}>{IC.chevR}</button></div>
    <div style={{display:"grid",gridTemplateColumns:"repeat(7,1fr)",gap:2,textAlign:"center"}}>{wd.map(w=><div key={w} style={{fontSize:10,color:"#98a2b3",fontWeight:500,padding:"3px 0"}}>{w}</div>)}{cells.map((d,i)=>(<div key={i} onClick={()=>d&&(()=>{onSel(new Date(vy,vm,d));onClose();})()} style={{width:32,height:32,display:"flex",alignItems:"center",justifyContent:"center",fontSize:12,borderRadius:8,cursor:d?"pointer":"default",fontWeight:isSel(d)?600:isNow(d)?500:400,background:isSel(d)?"#1a1d23":"transparent",color:isSel(d)?"#fff":isNow(d)?"#3b82f6":d?"#344054":"transparent",transition:"all .15s"}} onMouseEnter={e=>{if(d&&!isSel(d))e.currentTarget.style.background="#f0f2f5";}} onMouseLeave={e=>{if(d&&!isSel(d))e.currentTarget.style.background="transparent";}}>{d||""}</div>))}</div></div>);}

function Calc({value,onChange,onClose}){const[d,setD]=useState(value||"0");const[pv,setPv]=useState(null);const[op,setOp]=useState(null);const[fr,setFr]=useState(true);
  const num=n=>{if(fr){setD(String(n));setFr(false);}else setD(x=>x==="0"?String(n):x+n);};const dot=()=>{if(fr){setD("0.");setFr(false);return;}if(!d.includes("."))setD(x=>x+".");};
  const doOp=o=>{const c=parseFloat(d)||0;if(pv!==null&&op&&!fr){const r=ev(pv,c,op);setD(String(r));setPv(r);}else setPv(c);setOp(o);setFr(true);};
  const ev=(a,b,o)=>{if(o==="+")return a+b;if(o==="−")return a-b;if(o==="×")return a*b;if(o==="÷"&&b)return a/b;return b;};
  const eq=()=>{if(pv!==null&&op){const r=ev(pv,parseFloat(d)||0,op);setD(String(r));setPv(null);setOp(null);setFr(true);}};
  const clr=()=>{setD("0");setPv(null);setOp(null);setFr(true);};const bk=()=>setD(x=>x.length>1?x.slice(0,-1):"0");
  const done=()=>{let f;if(pv!==null&&op)f=ev(pv,parseFloat(d)||0,op);else f=parseFloat(d)||0;onChange(String(Math.round(f*100)/100));onClose();};
  const B=(l,a,s={})=>(<button onClick={a} className="cb" style={{width:"100%",height:40,border:"none",borderRadius:9,fontSize:15,fontWeight:500,fontFamily:"'JetBrains Mono',monospace",cursor:"pointer",background:"#f0f2f5",color:"#1a1d23",...s}}>{l}</button>);
  return(<div style={{marginTop:10,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:14,animation:"scaleIn .2s cubic-bezier(.34,1.56,.64,1)",boxShadow:"0 8px 32px rgba(0,0,0,.08)"}}>
    <div style={{background:"#f8f9fb",borderRadius:9,padding:"10px 14px",marginBottom:10,fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:600,textAlign:"right",color:"#1a1d23"}}>{op&&<span style={{fontSize:11,color:"#98a2b3",marginRight:6}}>{pv} {op}</span>}{d}</div>
    <div style={{display:"grid",gridTemplateColumns:"1fr 1fr 1fr 1fr",gap:5}}>
      {B("C",clr,{background:"#fef2f2",color:"#ef4444"})}{B(<span style={{display:"flex",justifyContent:"center"}}>{IC.bksp}</span>,bk)}{B("÷",()=>doOp("÷"),{background:"#eff6ff",color:"#3b82f6"})}{B("×",()=>doOp("×"),{background:"#eff6ff",color:"#3b82f6"})}
      {[7,8,9].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("−",()=>doOp("−"),{background:"#eff6ff",color:"#3b82f6"})}
      {[4,5,6].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("+",()=>doOp("+"),{background:"#eff6ff",color:"#3b82f6"})}
      {[1,2,3].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("=",eq,{background:"#1a1d23",color:"#fff"})}
      <span style={{gridColumn:"span 2"}}>{B(0,()=>num(0),{width:"100%"})}</span>{B(".",dot)}{B("✓",done,{background:"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",fontSize:16})}
    </div></div>);}

/* ====== COLOR PICKER ====== */
function ColorPick({current,onSelect,onClose}){
  const ref=useRef(null);
  const[hue,setHue]=useState(0);
  const[sat,setSat]=useState(80);
  const[lit,setLit]=useState(55);
  const[custom,setCustom]=useState(false);
  useEffect(()=>{const h=e=>{if(ref.current&&!ref.current.contains(e.target))onClose();};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[onClose]);

  const hslToHex=(h,s,l)=>{s/=100;l/=100;const a=s*Math.min(l,1-l);const f=n=>{const k=(n+h/30)%12;const c=l-a*Math.max(Math.min(k-3,9-k,1),-1);return Math.round(255*c).toString(16).padStart(2,"0");};return`#${f(0)}${f(8)}${f(4)}`;};

  return(<div ref={ref} style={{position:"absolute",left:28,top:0,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:14,width:200,zIndex:20,boxShadow:"0 12px 40px rgba(0,0,0,.15)",animation:"scaleIn .15s cubic-bezier(.34,1.56,.64,1)"}}>
    <p style={{fontSize:10,fontWeight:600,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Цвет</p>
    <div style={{display:"flex",flexWrap:"wrap",gap:6,marginBottom:custom?10:0}}>
      {PALETTE.map(c=>(<div key={c} onClick={()=>{onSelect(c);onClose();}} style={{width:24,height:24,borderRadius:7,background:c,cursor:"pointer",border:c===current?"2.5px solid #1a1d23":"2.5px solid transparent",transition:"transform .1s"}} onMouseEnter={e=>e.currentTarget.style.transform="scale(1.15)"} onMouseLeave={e=>e.currentTarget.style.transform="scale(1)"}/>))}
    </div>
    {!custom&&<button onClick={()=>setCustom(true)} style={{marginTop:8,border:"none",background:"none",cursor:"pointer",fontSize:11,color:"#3b82f6",fontWeight:500,fontFamily:"inherit",padding:0}}>Свой цвет...</button>}
    {custom&&(<div style={{animation:"fadeIn .15s"}}>
      <div style={{width:"100%",height:24,borderRadius:6,marginBottom:6,background:`linear-gradient(to right,hsl(0,80%,55%),hsl(60,80%,55%),hsl(120,80%,55%),hsl(180,80%,55%),hsl(240,80%,55%),hsl(300,80%,55%),hsl(360,80%,55%))`,position:"relative",cursor:"pointer"}} onClick={e=>{const r=e.currentTarget.getBoundingClientRect();setHue(Math.round(((e.clientX-r.left)/r.width)*360));}}>
        <div style={{position:"absolute",left:`${(hue/360)*100}%`,top:-2,width:4,height:28,background:"#fff",borderRadius:2,boxShadow:"0 1px 4px rgba(0,0,0,.3)",transform:"translateX(-50%)",pointerEvents:"none"}}/>
      </div>
      <div style={{display:"flex",gap:6,marginBottom:6}}>
        <div style={{flex:1}}>
          <label style={{fontSize:9,color:"#98a2b3"}}>Насыщ.</label>
          <input type="range" min="10" max="100" value={sat} onChange={e=>setSat(+e.target.value)} style={{width:"100%",height:4,accentColor:"#3b82f6"}}/>
        </div>
        <div style={{flex:1}}>
          <label style={{fontSize:9,color:"#98a2b3"}}>Яркость</label>
          <input type="range" min="25" max="75" value={lit} onChange={e=>setLit(+e.target.value)} style={{width:"100%",height:4,accentColor:"#3b82f6"}}/>
        </div>
      </div>
      <div style={{display:"flex",alignItems:"center",gap:8}}>
        <div style={{width:28,height:28,borderRadius:7,background:hslToHex(hue,sat,lit),border:"1px solid #e8eaed"}}/>
        <span style={{fontSize:11,fontFamily:"'JetBrains Mono',monospace",color:"#667085"}}>{hslToHex(hue,sat,lit)}</span>
        <button onClick={()=>{onSelect(hslToHex(hue,sat,lit));onClose();}} style={{marginLeft:"auto",border:"none",background:"#1a1d23",color:"#fff",fontSize:10,fontWeight:600,padding:"4px 10px",borderRadius:6,cursor:"pointer",fontFamily:"inherit"}}>OK</button>
      </div>
    </div>)}
  </div>);}

/* ====== CATEGORY MANAGER ====== */
function CatManager({cats,onUpdate,onClose}){
  const[list,setList]=useState(cats.map(c=>({...c})));
  const[adding,setAdding]=useState(false);
  const[newName,setNewName]=useState("");
  const[newColor,setNewColor]=useState(PALETTE[0]);
  const[editId,setEditId]=useState(null);
  const[editName,setEditName]=useState("");
  const[delConfirm,setDelConfirm]=useState(null);
  const[colorPickId,setColorPickId]=useState(null);
  const[dragId,setDragId]=useState(null);
  const[dragOver,setDragOver]=useState(null);

  const save=()=>{onUpdate(list);onClose();};
  const addCat=()=>{if(!newName.trim())return;if(list.find(c=>c.name.toLowerCase()===newName.trim().toLowerCase()))return;
    setList(p=>[...p,{id:Date.now(),name:newName.trim(),color:newColor}]);setNewName("");setAdding(false);setNewColor(PALETTE[(list.length)%PALETTE.length]);};
  const delCat=id=>{setList(p=>p.filter(c=>c.id!==id));setDelConfirm(null);};
  const renameCat=(id,name)=>{if(!name.trim())return;setList(p=>p.map(c=>c.id===id?{...c,name:name.trim()}:c));setEditId(null);};
  const recolorCat=(id,color)=>{setList(p=>p.map(c=>c.id===id?{...c,color}:c));setColorPickId(null);};

  const onDragStart=(e,id)=>{setDragId(id);e.dataTransfer.effectAllowed="move";};
  const onDragOver=(e,id)=>{e.preventDefault();if(id!==dragId)setDragOver(id);};
  const onDrop=(e,targetId)=>{e.preventDefault();if(!dragId||dragId===targetId)return;
    setList(prev=>{const items=[...prev];const fromIdx=items.findIndex(c=>c.id===dragId);const toIdx=items.findIndex(c=>c.id===targetId);const[moved]=items.splice(fromIdx,1);items.splice(toIdx,0,moved);return items;});
    setDragId(null);setDragOver(null);};
  const onDragEnd=()=>{setDragId(null);setDragOver(null);};

  return(<div style={{position:"fixed",inset:0,background:"rgba(0,0,0,.35)",backdropFilter:"blur(2px)",display:"flex",alignItems:"center",justifyContent:"center",zIndex:150,animation:"backdropIn .15s"}}>
    <div style={{background:"#fff",borderRadius:20,width:380,maxHeight:"80vh",display:"flex",flexDirection:"column",animation:"modalIn .3s cubic-bezier(.34,1.56,.64,1)",boxShadow:"0 24px 80px rgba(0,0,0,.18)"}}>
      <div style={{padding:"22px 22px 16px",borderBottom:"1px solid #f0f2f5",display:"flex",justifyContent:"space-between",alignItems:"center"}}>
        <h3 style={{fontSize:16,fontWeight:600}}>Управление категориями</h3>
        <button onClick={save} style={{border:"none",background:"none",cursor:"pointer",color:"#98a2b3",display:"flex",padding:4,borderRadius:8,transition:"color .15s"}} onMouseEnter={e=>e.currentTarget.style.color="#667085"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.close}</button>
      </div>
      <div style={{padding:"8px 22px 0"}}><p style={{fontSize:11,color:"#98a2b3",lineHeight:1.4}}>Перетащите для сортировки. Первые 4 — быстрый доступ.</p></div>
      <div style={{flex:1,overflowY:"auto",padding:"8px 22px"}}>
        {list.map((cat,i)=>(
          <div key={cat.id}
            draggable={editId!==cat.id}
            onDragStart={e=>onDragStart(e,cat.id)}
            onDragOver={e=>onDragOver(e,cat.id)}
            onDrop={e=>onDrop(e,cat.id)}
            onDragEnd={onDragEnd}
            style={{
              display:"flex",alignItems:"center",gap:10,padding:"9px 4px",
              borderBottom:i<list.length-1?"1px solid #f8f9fb":"none",
              position:"relative",
              background:dragOver===cat.id?"#eff6ff":dragId===cat.id?"#f8f9fb":"transparent",
              borderTop:dragOver===cat.id?"2px solid #3b82f6":"2px solid transparent",
              opacity:dragId===cat.id?.5:1,
              cursor:editId===cat.id?"text":"grab",
              transition:"background .15s, opacity .15s",
              borderRadius:6,
            }}>
            {/* Drag handle */}
            <div style={{display:"flex",flexDirection:"column",gap:2,cursor:"grab",padding:"0 2px",flexShrink:0,color:"#d0d5dd"}}>
              <div style={{display:"flex",gap:2}}><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/></div>
              <div style={{display:"flex",gap:2}}><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/></div>
              <div style={{display:"flex",gap:2}}><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/><div style={{width:3,height:3,borderRadius:"50%",background:"currentColor"}}/></div>
            </div>
            {/* Color dot */}
            <div onClick={()=>setColorPickId(colorPickId===cat.id?null:cat.id)} style={{width:22,height:22,borderRadius:7,background:cat.color,cursor:"pointer",flexShrink:0,transition:"transform .15s",border:"2px solid rgba(0,0,0,.06)"}} onMouseEnter={e=>e.currentTarget.style.transform="scale(1.15)"} onMouseLeave={e=>e.currentTarget.style.transform="scale(1)"}/>
            {/* Color picker */}
            {colorPickId===cat.id&&<ColorPick current={cat.color} onSelect={c=>recolorCat(cat.id,c)} onClose={()=>setColorPickId(null)}/>}
            {/* Name */}
            {editId===cat.id?(
              <input autoFocus value={editName} onChange={e=>setEditName(e.target.value)} onKeyDown={e=>{if(e.key==="Enter")renameCat(cat.id,editName);if(e.key==="Escape")setEditId(null);}} onBlur={()=>renameCat(cat.id,editName)} style={{flex:1,border:"none",borderBottom:"1.5px solid #3b82f6",outline:"none",fontSize:13,fontWeight:450,color:"#1a1d23",padding:"2px 0",background:"transparent",fontFamily:"inherit"}}/>
            ):(
              <span onClick={()=>{setEditId(cat.id);setEditName(cat.name);}} style={{flex:1,fontSize:13,fontWeight:450,color:"#344054",cursor:"pointer",transition:"color .15s",userSelect:"none"}} onMouseEnter={e=>e.currentTarget.style.color="#3b82f6"} onMouseLeave={e=>e.currentTarget.style.color="#344054"}>{cat.name}</span>
            )}
            {i<4&&<span style={{fontSize:9,fontWeight:600,color:"#22c55e",background:"#f0fdf4",padding:"2px 6px",borderRadius:4,flexShrink:0,userSelect:"none"}}>TOP</span>}
            {delConfirm===cat.id?(
              <div style={{display:"flex",gap:4,flexShrink:0}}>
                <button onClick={()=>delCat(cat.id)} style={{border:"none",background:"#ef4444",color:"#fff",fontSize:10,fontWeight:600,padding:"4px 8px",borderRadius:5,cursor:"pointer",fontFamily:"inherit"}}>Да</button>
                <button onClick={()=>setDelConfirm(null)} style={{border:"none",background:"#f0f2f5",color:"#667085",fontSize:10,fontWeight:600,padding:"4px 8px",borderRadius:5,cursor:"pointer",fontFamily:"inherit"}}>Нет</button>
              </div>
            ):(
              <button onClick={()=>setDelConfirm(cat.id)} style={{border:"none",background:"none",cursor:"pointer",color:"#d0d5dd",display:"flex",padding:3,borderRadius:5,transition:"color .15s",flexShrink:0}} onMouseEnter={e=>e.currentTarget.style.color="#ef4444"} onMouseLeave={e=>e.currentTarget.style.color="#d0d5dd"}>{IC.trash}</button>
            )}
          </div>
        ))}
        {adding?(
          <div style={{display:"flex",alignItems:"center",gap:10,padding:"10px 4px",animation:"fadeIn .2s"}}>
            <div style={{width:14}}/>
            <div onClick={()=>setNewColor(PALETTE[(PALETTE.indexOf(newColor)+1)%PALETTE.length])} style={{width:22,height:22,borderRadius:7,background:newColor,cursor:"pointer",flexShrink:0,border:"2px solid rgba(0,0,0,.06)"}}/>
            <input autoFocus value={newName} onChange={e=>setNewName(e.target.value)} onKeyDown={e=>{if(e.key==="Enter")addCat();if(e.key==="Escape"){setAdding(false);setNewName("");}}} placeholder="Название..." style={{flex:1,border:"none",borderBottom:"1.5px solid #22c55e",outline:"none",fontSize:13,color:"#1a1d23",padding:"2px 0",background:"transparent",fontFamily:"inherit"}}/>
            <button onClick={addCat} style={{border:"none",background:"#22c55e",color:"#fff",display:"flex",alignItems:"center",justifyContent:"center",width:24,height:24,borderRadius:6,cursor:"pointer"}}>{IC.check}</button>
            <button onClick={()=>{setAdding(false);setNewName("");}} style={{border:"none",background:"#f0f2f5",color:"#667085",display:"flex",alignItems:"center",justifyContent:"center",width:24,height:24,borderRadius:6,cursor:"pointer"}}>{IC.close}</button>
          </div>
        ):(
          <button onClick={()=>{setAdding(true);setNewColor(PALETTE[list.length%PALETTE.length]);}} style={{display:"flex",alignItems:"center",gap:6,padding:"10px 4px",border:"none",background:"none",cursor:"pointer",fontSize:12,fontWeight:500,color:"#3b82f6",fontFamily:"inherit",transition:"color .15s"}} onMouseEnter={e=>e.currentTarget.style.color="#2563eb"} onMouseLeave={e=>e.currentTarget.style.color="#3b82f6"}><span style={{display:"flex"}}>{IC.plusSm}</span>Добавить категорию</button>
        )}
      </div>
      <div style={{padding:"14px 22px",borderTop:"1px solid #f0f2f5",display:"flex",justifyContent:"flex-end"}}>
        <button onClick={save} style={{padding:"8px 20px",borderRadius:9,border:"none",background:"#1a1d23",color:"#fff",fontSize:13,fontWeight:600,cursor:"pointer",fontFamily:"inherit",transition:"all .15s"}} onMouseEnter={e=>e.currentTarget.style.background="#2a2d35"} onMouseLeave={e=>e.currentTarget.style.background="#1a1d23"}>Сохранить</button>
      </div>
    </div>
  </div>);}

function ANum({value,color,sz=38}){const[d,setD]=useState(value);const pv=useRef(value);const[fl,setFl]=useState(false);
  useEffect(()=>{if(pv.current!==value){setFl(true);const st=pv.current,df=value-st,steps=20;let s=0;const iv=setInterval(()=>{s++;setD(Math.round(st+df*(1-Math.pow(1-s/steps,3))));if(s>=steps){clearInterval(iv);setD(value);pv.current=value;}},20);setTimeout(()=>setFl(false),500);return()=>clearInterval(iv);}},[value]);
  return(<span style={{fontFamily:"'JetBrains Mono',monospace",fontWeight:700,fontSize:sz,color,transition:"transform .3s",display:"inline-block",transform:fl?"scale(1.03)":"scale(1)",letterSpacing:"-1px",lineHeight:1}}>{fm(d)}</span>);}

/* ====== MAIN ====== */
export default function App(){
  const[txList,setTxList]=useState(initialTx);
  const[cats,setCats]=useState(initCats);
  const[period,setPeriod]=useState("Месяц");
  const[modal,setModal]=useState(false);
  const[catMgr,setCatMgr]=useState(false);
  const[inlineAdd,setInlineAdd]=useState(false);
  const[inlineName,setInlineName]=useState("");
  const[aType,setAType]=useState("expense");
  const[aAmt,setAAmt]=useState("");
  const[aCat,setACat]=useState("");
  const[aDesc,setADesc]=useState("");
  const[aDate,setADate]=useState(new Date());
  const[aWallet,setAWallet]=useState("cash");
  const[sCalc,setSCalc]=useState(false);
  const[sCal,setSCal]=useState(false);
  const[sAllCats,setSAllCats]=useState(false);
  const[ctx,setCtx]=useState(null);
  const[toasts,setToasts]=useState([]);
  const[newId,setNewId]=useState(null);

  const periods=["Сегодня","Неделя","Месяц","Год","Всё время"];
  const catColors=Object.fromEntries(cats.map(c=>[c.name,c.color]));
  Object.assign(catColors,extraCatColors);
  const allCats=cats.map(c=>c.name);
  const topCats=allCats.slice(0,4);
  const inc=txList.filter(t=>t.type==="income").reduce((s,t)=>s+t.amount,0);
  const exp=Math.abs(txList.filter(t=>t.type==="expense").reduce((s,t)=>s+t.amount,0));
  const bal=inc-exp;

  // Day status
  const todayTx=txList.filter(t=>t.date===todayStr);
  const todayInc=todayTx.filter(t=>t.type==="income").reduce((s,t)=>s+t.amount,0);
  const todayExp=Math.abs(todayTx.filter(t=>t.type==="expense").reduce((s,t)=>s+t.amount,0));
  const todayDelta=todayInc-todayExp;

  // Top expense categories
  const expByCat={};txList.filter(t=>t.type==="expense").forEach(t=>{expByCat[t.cat]=(expByCat[t.cat]||0)+Math.abs(t.amount);});
  const topExp=Object.entries(expByCat).sort((a,b)=>b[1]-a[1]).slice(0,4);
  const maxExp=topExp[0]?topExp[0][1]:1;

  // Top income categories
  const incByCat={};txList.filter(t=>t.type==="income").forEach(t=>{incByCat[t.cat]=(incByCat[t.cat]||0)+t.amount;});
  const topInc=Object.entries(incByCat).sort((a,b)=>b[1]-a[1]).slice(0,3);

  // Group by day
  const groups={};txList.forEach(t=>{if(!groups[t.date])groups[t.date]=[];groups[t.date].push(t);});
  const sortedDays=Object.keys(groups).sort((a,b)=>{const[ad,am,ay]=a.split(".");const[bd,bm,by]=b.split(".");return new Date(by,bm-1,bd)-new Date(ay,am-1,ad);});
  const dayLabel=ds=>{if(ds===todayStr)return"Сегодня";const[d,m,y]=ds.split(".");const dt=new Date(y,m-1,d);const diff=Math.floor((today-dt)/(86400000));if(diff===1)return"Вчера";const mo=["янв","фев","мар","апр","мая","июн","июл","авг","сен","окт","ноя","дек"];return`${d} ${mo[parseInt(m)-1]}`;};

  const toast=useCallback((a,ti,m,u)=>{const id=++tid;setToasts(p=>[...p,{id,action:a,title:ti,message:m,undoData:u}]);},[]);
  const dismiss=useCallback(id=>setToasts(p=>p.filter(t=>t.id!==id)),[]);
  const undo=useCallback(t=>{if(t.undoData?.type==="delete")setTxList(p=>[...p,t.undoData.tx].sort((a,b)=>b.id-a.id));else if(t.undoData?.type==="add")setTxList(p=>p.filter(x=>x.id!==t.undoData.txId));toast("add","Отменено","Операция восстановлена",null);},[toast]);
  const del=tx=>{setTxList(p=>p.filter(t=>t.id!==tx.id));toast("delete","Удалено",`${tx.cat} — ${Math.abs(tx.amount).toLocaleString("ru-RU")} ₽`,{type:"delete",tx});};
  const add=()=>{const a=parseFloat(aAmt)||0;if(a<=0)return;const n={id:Date.now(),date:fmtD(aDate),type:aType,cat:aCat||"Другое",amount:aType==="expense"?-a:a,wallet:aWallet};setTxList(p=>[n,...p]);setNewId(n.id);setTimeout(()=>setNewId(null),2000);toast("add","Добавлено",`${n.cat} — ${a.toLocaleString("ru-RU")} ₽`,{type:"add",txId:n.id});setModal(false);};
  const onCtx=useCallback((e,tx)=>{e.preventDefault();setCtx({x:e.clientX,y:e.clientY,tx});},[]);
  const inlineAddCat=()=>{const nm=inlineName.trim();if(!nm){setInlineAdd(false);return;}const existing=cats.find(c=>c.name.toLowerCase()===nm.toLowerCase());if(existing){setACat(existing.name);}else{const nc={id:Date.now(),name:nm,color:PALETTE[cats.length%PALETTE.length]};setCats(p=>[...p,nc]);setACat(nm);}setInlineName("");setInlineAdd(false);};
  const reset=()=>{setAAmt("");setACat("");setADesc("");setADate(new Date());setAWallet("cash");setSCalc(false);setSCal(false);setSAllCats(false);};

  return(
    <div style={{display:"flex",height:"100vh",fontFamily:"'DM Sans','Segoe UI',sans-serif",background:"#f0f2f5",color:"#1a1d23",overflow:"hidden"}}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,300;0,9..40,400;0,9..40,500;0,9..40,600;0,9..40,700&family=JetBrains+Mono:wght@400;500;600;700&display=swap');
        *{box-sizing:border-box;margin:0;padding:0}::-webkit-scrollbar{width:5px}::-webkit-scrollbar-track{background:transparent}::-webkit-scrollbar-thumb{background:#d0d5dd;border-radius:3px}
        @keyframes fadeIn{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:translateY(0)}}
        @keyframes scaleIn{from{opacity:0;transform:scale(.92)}to{opacity:1;transform:scale(1)}}
        @keyframes modalIn{from{opacity:0;transform:scale(.92) translateY(12px)}to{opacity:1;transform:scale(1) translateY(0)}}
        @keyframes backdropIn{from{opacity:0}to{opacity:1}}
        @keyframes tipIn{from{opacity:0;transform:translateX(-4px) translateY(-50%)}to{opacity:1;transform:translateX(0) translateY(-50%)}}
        @keyframes toastIn{from{opacity:0;transform:translateY(16px) scale(.9)}to{opacity:1;transform:translateY(0) scale(1)}}
        @keyframes toastOut{from{opacity:1;transform:translateY(0) scale(1)}to{opacity:0;transform:translateY(-8px) scale(.95)}}
        @keyframes rowIn{from{opacity:0;transform:translateX(-10px)}to{opacity:1;transform:translateX(0)}}
        @keyframes highlight{0%{background:rgba(34,197,94,.12)}100%{background:transparent}}
        @keyframes barGrow{from{width:0}to{width:var(--tw)}}
        @keyframes pulseOnce{0%{box-shadow:0 0 0 0 rgba(34,197,94,.25)}100%{box-shadow:0 0 0 12px rgba(34,197,94,0)}}
        .ni{transition:all .2s;cursor:pointer;position:relative}.ni:hover{background:rgba(255,255,255,.08)!important}.ni.a{background:rgba(255,255,255,.12)!important}.ni.a::before{content:'';position:absolute;left:0;top:50%;transform:translateY(-50%);width:3px;height:22px;background:#22c55e;border-radius:0 3px 3px 0}
        .pb{transition:all .2s;cursor:pointer;border:none;background:none;font-family:inherit}.pb:hover{color:#1a1d23!important;background:#e8eaed!important}
        .cd{background:#fff;border-radius:14px;border:1px solid #e8eaed;transition:all .25s}
        .tr{transition:all .15s;cursor:default}.tr:hover{background:#f8f9fb!important}
        .fb{transition:all .2s cubic-bezier(.34,1.56,.64,1);cursor:pointer}.fb:hover{transform:scale(1.08);box-shadow:0 8px 28px rgba(34,197,94,.35)!important}.fb:active{transform:scale(.95)}
        .tb{transition:all .2s;cursor:pointer;border:none;font-family:inherit}
        .cb{transition:all .1s}.cb:hover{filter:brightness(.93)}.cb:active{transform:scale(.96)}
        .mi{font-family:'JetBrains Mono',monospace;font-size:30px;font-weight:600;border:none;outline:none;width:100%;text-align:center;color:#1a1d23;background:transparent}.mi::placeholder{color:#d0d5dd}
        .cc{transition:all .15s;cursor:pointer;border:none;font-family:inherit}.cc:hover{transform:translateY(-1px);box-shadow:0 2px 8px rgba(0,0,0,.06)}
        .sb{transition:all .2s;cursor:pointer;border:none;font-family:inherit}.sb:hover{transform:translateY(-1px);box-shadow:0 4px 12px rgba(34,197,94,.25)}.sb:active{transform:translateY(0)}
        .wb{transition:all .2s;cursor:pointer;border:none;font-family:inherit}.wb:hover{transform:translateY(-1px)}
      `}</style>

      {/* Sidebar */}
      <nav style={{width:64,background:"linear-gradient(180deg,#1a1d23,#24272e)",display:"flex",flexDirection:"column",flexShrink:0,zIndex:10}}>
        <div style={{padding:"18px 0",display:"flex",alignItems:"center",justifyContent:"center",borderBottom:"1px solid rgba(255,255,255,.06)"}}><div style={{width:34,height:34,borderRadius:9,background:"linear-gradient(135deg,#22c55e,#16a34a)",display:"flex",alignItems:"center",justifyContent:"center",fontSize:15,fontWeight:700,color:"#fff"}}>₽</div></div>
        <div style={{padding:"10px 7px",flex:1,display:"flex",flexDirection:"column",gap:2}}>{navItems.map(i=>(<Tip key={i.label} text={i.label}><div className={`ni ${i.active?"a":""}`} style={{display:"flex",alignItems:"center",justifyContent:"center",padding:"9px 0",borderRadius:9,color:i.active?"#fff":"rgba(255,255,255,.45)"}}>{IC[i.icon]}</div></Tip>))}</div>
        <div style={{padding:"8px 7px 14px",display:"flex",flexDirection:"column",gap:6,alignItems:"center"}}>
          <Tip text="Заблокировать"><button style={{border:"none",background:"rgba(255,255,255,.06)",borderRadius:8,width:34,height:34,display:"flex",alignItems:"center",justifyContent:"center",color:"rgba(255,255,255,.35)",cursor:"pointer",transition:"all .2s"}} onMouseEnter={e=>{e.currentTarget.style.background="rgba(255,255,255,.12)";e.currentTarget.style.color="rgba(255,255,255,.6)";}} onMouseLeave={e=>{e.currentTarget.style.background="rgba(255,255,255,.06)";e.currentTarget.style.color="rgba(255,255,255,.35)";}}>{IC.lock}</button></Tip>
        </div>
      </nav>

      {/* Main */}
      <main style={{flex:1,overflow:"auto",padding:"24px 28px",position:"relative"}}>
        {/* HEADER ROW: Title + Day Status + Periods */}
        <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:22,animation:"fadeIn .3s"}}>
          <div style={{display:"flex",alignItems:"center",gap:16}}>
            <h1 style={{fontSize:24,fontWeight:700,letterSpacing:"-.5px"}}>Главная</h1>
            {/* Day Status Pill */}
            <div style={{display:"flex",alignItems:"center",gap:6,padding:"5px 12px",borderRadius:20,background:todayTx.length===0?"#f8f9fb":todayDelta>=0?"#f0fdf4":"#fef2f2",border:`1px solid ${todayTx.length===0?"#e8eaed":todayDelta>=0?"#bbf7d0":"#fecaca"}`}}>
              <div style={{width:7,height:7,borderRadius:"50%",background:todayTx.length===0?"#98a2b3":todayDelta>=0?"#22c55e":"#ef4444"}}/>
              <span style={{fontSize:12,fontWeight:600,color:todayTx.length===0?"#98a2b3":todayDelta>=0?"#15803d":"#dc2626"}}>{todayTx.length===0?"Нет операций":fm(todayDelta)+" ₽ сегодня"}</span>
            </div>
          </div>
          <div style={{display:"flex",background:"#fff",borderRadius:9,padding:3,border:"1px solid #e8eaed"}}>{periods.map(p=>(<button key={p} className="pb" onClick={()=>setPeriod(p)} style={{padding:"6px 12px",borderRadius:7,fontSize:12,fontWeight:period===p?600:400,color:period===p?"#fff":"#667085",background:period===p?"#1a1d23":"transparent"}}>{p}</button>))}</div>
        </div>

        {/* COMPACT BALANCE ROW */}
        <div style={{display:"grid",gridTemplateColumns:"1fr 1fr 1fr 1fr",gap:14,marginBottom:22,animation:"fadeIn .4s"}}>
          {/* Balance - spans more visually */}
          <div className="cd" style={{padding:"20px 22px",background:bal>=0?"linear-gradient(135deg,#f0fdf4,#dcfce7 60%,#f0fdf4)":"linear-gradient(135deg,#fef2f2,#fecaca 60%,#fef2f2)",border:bal>=0?"1px solid #bbf7d0":"1px solid #fecaca",position:"relative",overflow:"hidden"}}>
            <p style={{fontSize:10,fontWeight:600,textTransform:"uppercase",letterSpacing:"1px",color:bal>=0?"#15803d":"#b91c1c",marginBottom:6}}>Баланс</p>
            <ANum value={bal} color={bal>=0?"#15803d":"#dc2626"} sz={32}/>
            <span style={{fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:500,color:bal>=0?"#15803d":"#dc2626"}}> ₽</span>
          </div>
          {/* Income */}
          <div className="cd" style={{padding:"20px 22px"}}>
            <div style={{display:"flex",alignItems:"center",gap:7,marginBottom:8}}><div style={{width:28,height:28,borderRadius:8,background:"#f0fdf4",display:"flex",alignItems:"center",justifyContent:"center",color:"#22c55e"}}>{IC.arrowUp}</div><span style={{fontSize:11,color:"#667085",fontWeight:500}}>Доходы</span></div>
            <div style={{fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:600,color:"#22c55e"}}>+{inc.toLocaleString("ru-RU")} <span style={{fontSize:13}}>₽</span></div>
          </div>
          {/* Expense */}
          <div className="cd" style={{padding:"20px 22px"}}>
            <div style={{display:"flex",alignItems:"center",gap:7,marginBottom:8}}><div style={{width:28,height:28,borderRadius:8,background:"#fef2f2",display:"flex",alignItems:"center",justifyContent:"center",color:"#ef4444"}}>{IC.arrowDown}</div><span style={{fontSize:11,color:"#667085",fontWeight:500}}>Расходы</span></div>
            <div style={{fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:600,color:"#ef4444"}}>−{exp.toLocaleString("ru-RU")} <span style={{fontSize:13}}>₽</span></div>
          </div>
          {/* Delta */}
          <div className="cd" style={{padding:"20px 22px"}}>
            <div style={{display:"flex",alignItems:"center",gap:7,marginBottom:8}}><div style={{width:28,height:28,borderRadius:8,background:"#eff6ff",display:"flex",alignItems:"center",justifyContent:"center",color:"#3b82f6"}}>{IC.info}</div><span style={{fontSize:11,color:"#667085",fontWeight:500}}>За период</span></div>
            <div style={{fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:600,color:bal>=0?"#22c55e":"#ef4444"}}>{fm(inc-exp)} <span style={{fontSize:13}}>₽</span></div>
          </div>
        </div>

        {/* TWO COLUMN LAYOUT */}
        <div style={{display:"grid",gridTemplateColumns:"1fr 320px",gap:18,animation:"fadeIn .5s"}}>
          {/* LEFT: Grouped Transactions */}
          <div className="cd" style={{overflow:"hidden",minHeight:400}}>
            <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",padding:"16px 20px 12px",borderBottom:"1px solid #f0f2f5"}}>
              <h2 style={{fontSize:15,fontWeight:600}}>Операции</h2>
              <button style={{fontSize:12,color:"#3b82f6",background:"none",border:"none",cursor:"pointer",fontFamily:"inherit",fontWeight:500,display:"flex",alignItems:"center",gap:3}}>Все {IC.chevron}</button>
            </div>
            <div style={{maxHeight:520,overflowY:"auto"}}>
            {sortedDays.map((day,di)=>{
              const dayTx=groups[day];
              const dayInc=dayTx.filter(t=>t.type==="income").reduce((s,t)=>s+t.amount,0);
              const dayExp=Math.abs(dayTx.filter(t=>t.type==="expense").reduce((s,t)=>s+t.amount,0));
              const dayD=dayInc-dayExp;
              return(
                <div key={day} style={{animation:`fadeIn ${.2+di*.08}s ease both`}}>
                  {/* Day Header */}
                  <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",padding:"10px 20px 6px",background:"#f8f9fb",borderBottom:"1px solid #f0f2f5"}}>
                    <span style={{fontSize:12,fontWeight:600,color:"#344054"}}>{dayLabel(day)}</span>
                    <span style={{fontSize:11,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:dayD>=0?"#22c55e":"#ef4444"}}>{fm(dayD)} ₽</span>
                  </div>
                  {/* Transactions */}
                  {dayTx.map((tx,ti)=>(
                    <div key={tx.id} className="tr" onContextMenu={e=>onCtx(e,tx)}
                      style={{display:"grid",gridTemplateColumns:"1fr 40px 120px",padding:"11px 20px",alignItems:"center",borderBottom:"1px solid #f8f9fb",
                        animation:tx.id===newId?"highlight 2s ease":"rowIn .25s ease both",animationDelay:tx.id===newId?"0s":`${ti*.04}s`}}>
                      <div style={{display:"flex",alignItems:"center",gap:9}}>
                        <div style={{width:7,height:7,borderRadius:"50%",background:tx.type==="income"?"#22c55e":"#ef4444",flexShrink:0}}/>
                        <span style={{fontSize:13,fontWeight:450,color:"#344054"}}>{tx.cat}</span>
                      </div>
                      <span style={{display:"flex",color:tx.wallet==="cash"?"#98a2b3":"#93c5fd",justifyContent:"center"}}>{tx.wallet==="cash"?IC.cash:IC.card}</span>
                      <span style={{fontSize:13,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:tx.type==="income"?"#22c55e":"#ef4444",textAlign:"right"}}>{tx.type==="income"?"+":"−"}{Math.abs(tx.amount).toLocaleString("ru-RU")} ₽</span>
                    </div>
                  ))}
                </div>
              );})}
            </div>
            <div style={{padding:"10px 20px",fontSize:11,color:"#98a2b3",borderTop:"1px solid #f0f2f5",textAlign:"center"}}>ПКМ → Редактировать · Дублировать · Удалить</div>
          </div>

          {/* RIGHT: Widgets Column */}
          <div style={{display:"flex",flexDirection:"column",gap:14}}>
            {/* Top Expense Categories */}
            <div className="cd" style={{padding:"18px 20px"}}>
              <h3 style={{fontSize:13,fontWeight:600,marginBottom:14}}>Топ расходов</h3>
              <div style={{display:"flex",flexDirection:"column",gap:10}}>
                {topExp.map(([cat,val],i)=>(
                  <div key={cat} style={{animation:`fadeIn ${.3+i*.1}s ease both`}}>
                    <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:5}}>
                      <div style={{display:"flex",alignItems:"center",gap:7}}>
                        <div style={{width:8,height:8,borderRadius:3,background:catColors[cat]||"#94a3b8"}}/>
                        <span style={{fontSize:12,fontWeight:450,color:"#344054"}}>{cat}</span>
                      </div>
                      <span style={{fontSize:12,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:"#344054"}}>{val.toLocaleString("ru-RU")} ₽</span>
                    </div>
                    <div style={{height:6,background:"#f0f2f5",borderRadius:3,overflow:"hidden"}}>
                      <div style={{height:"100%",borderRadius:3,background:catColors[cat]||"#94a3b8",width:`${(val/maxExp)*100}%`,animation:`barGrow .6s ease ${.2+i*.1}s both`,["--tw"]:`${(val/maxExp)*100}%`,opacity:.75}}/>
                    </div>
                  </div>
                ))}
              </div>
              <div style={{marginTop:12,paddingTop:10,borderTop:"1px solid #f0f2f5",fontSize:11,color:"#98a2b3"}}>
                Всего расходов: <span style={{fontWeight:600,color:"#344054",fontFamily:"'JetBrains Mono',monospace"}}>{exp.toLocaleString("ru-RU")} ₽</span>
              </div>
            </div>

            {/* Top Income Categories */}
            <div className="cd" style={{padding:"18px 20px"}}>
              <h3 style={{fontSize:13,fontWeight:600,marginBottom:14}}>Топ доходов</h3>
              <div style={{display:"flex",flexDirection:"column",gap:10}}>
                {topInc.map(([cat,val],i)=>{const maxI=topInc[0]?topInc[0][1]:1;return(
                  <div key={cat} style={{animation:`fadeIn ${.4+i*.1}s ease both`}}>
                    <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:5}}>
                      <div style={{display:"flex",alignItems:"center",gap:7}}><div style={{width:8,height:8,borderRadius:3,background:catColors[cat]||"#22c55e"}}/><span style={{fontSize:12,fontWeight:450,color:"#344054"}}>{cat}</span></div>
                      <span style={{fontSize:12,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:"#15803d"}}>{val.toLocaleString("ru-RU")} ₽</span>
                    </div>
                    <div style={{height:6,background:"#f0fdf4",borderRadius:3,overflow:"hidden"}}><div style={{height:"100%",borderRadius:3,background:catColors[cat]||"#22c55e",width:`${(val/maxI)*100}%`,animation:`barGrow .6s ease ${.3+i*.1}s both`,["--tw"]:`${(val/maxI)*100}%`,opacity:.65}}/></div>
                  </div>);})}
              </div>
              <div style={{marginTop:12,paddingTop:10,borderTop:"1px solid #f0f2f5",fontSize:11,color:"#98a2b3"}}>Всего доходов: <span style={{fontWeight:600,color:"#15803d",fontFamily:"'JetBrains Mono',monospace"}}>{inc.toLocaleString("ru-RU")} ₽</span></div>
            </div>

            {/* Mini Summary */}
            <div className="cd" style={{padding:"16px 20px",background:"linear-gradient(135deg,#fafafa,#f8f9fb)"}}>
              <h3 style={{fontSize:13,fontWeight:600,marginBottom:10}}>Сводка за период</h3>
              <div style={{display:"flex",flexDirection:"column",gap:6}}>
                <div style={{display:"flex",justifyContent:"space-between",fontSize:12}}><span style={{color:"#667085"}}>Операций</span><span style={{fontWeight:600}}>{txList.length}</span></div>
                <div style={{display:"flex",justifyContent:"space-between",fontSize:12}}><span style={{color:"#667085"}}>Дней с операциями</span><span style={{fontWeight:600}}>{sortedDays.length}</span></div>
                <div style={{display:"flex",justifyContent:"space-between",fontSize:12}}><span style={{color:"#667085"}}>Средний расход/день</span><span style={{fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:"#ef4444"}}>{sortedDays.length?Math.round(exp/sortedDays.length).toLocaleString("ru-RU"):0} ₽</span></div>
              </div>
            </div>
          </div>
        </div>
        <div style={{height:70}}/>
      </main>

      {/* FAB */}
      <button className="fb" onClick={()=>{reset();setModal(true);}} style={{position:"fixed",bottom:24,right:28,width:52,height:52,borderRadius:15,background:"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",border:"none",display:"flex",alignItems:"center",justifyContent:"center",boxShadow:"0 4px 18px rgba(34,197,94,.3)",zIndex:50}}>{IC.plus}</button>

      {ctx&&<CtxMenu x={ctx.x} y={ctx.y} onEdit={()=>{toast("edit","Отредактировано",`${ctx.tx.cat}`,{type:"edit",tx:{...ctx.tx}});}} onDup={()=>{const d={...ctx.tx,id:Date.now()};setTxList(p=>[d,...p]);setNewId(d.id);setTimeout(()=>setNewId(null),2000);toast("add","Дублировано",`${d.cat} — ${Math.abs(d.amount).toLocaleString("ru-RU")} ₽`,{type:"add",txId:d.id});}} onDel={()=>del(ctx.tx)} onClose={()=>setCtx(null)}/>}
      <Toasts toasts={toasts} onDismiss={dismiss} onUndo={undo}/>

      {/* Modal */}
      {modal&&(<div style={{position:"fixed",inset:0,background:"rgba(0,0,0,.4)",backdropFilter:"blur(4px)",display:"flex",alignItems:"center",justifyContent:"center",zIndex:100,animation:"backdropIn .2s"}} onClick={e=>{if(e.target===e.currentTarget)setModal(false);}}>
        <div style={{background:"#fff",borderRadius:22,width:420,maxHeight:"90vh",overflowY:"auto",padding:28,animation:"modalIn .3s cubic-bezier(.34,1.56,.64,1)",boxShadow:"0 24px 80px rgba(0,0,0,.15)"}}>
          <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:22}}><h3 style={{fontSize:17,fontWeight:600}}>Новая операция</h3><button onClick={()=>setModal(false)} style={{border:"none",background:"none",cursor:"pointer",color:"#98a2b3",display:"flex",padding:4,borderRadius:8}}>{IC.close}</button></div>
          <div style={{display:"flex",background:"#f0f2f5",borderRadius:11,padding:3,marginBottom:18}}>{["expense","income"].map(t=>(<button key={t} className="tb" onClick={()=>setAType(t)} style={{flex:1,padding:"9px",borderRadius:8,fontSize:13,fontWeight:500,color:aType===t?"#fff":"#667085",background:aType===t?(t==="expense"?"#ef4444":"#22c55e"):"transparent"}}>{t==="expense"?"Расход":"Доход"}</button>))}</div>
          <div style={{padding:18,background:"#f8f9fb",borderRadius:14,marginBottom:18}}>
            <div style={{display:"flex",alignItems:"center",gap:8}}><input className="mi" type="text" placeholder="0" value={aAmt} onChange={e=>setAAmt(e.target.value)} autoFocus style={{flex:1}}/><button onClick={()=>setSCalc(!sCalc)} style={{border:"none",borderRadius:9,width:38,height:38,background:sCalc?"#1a1d23":"#e8eaed",color:sCalc?"#fff":"#667085",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0,transition:"all .2s"}}>{IC.calc}</button></div>
            <p style={{textAlign:"center",fontSize:11,color:"#98a2b3",marginTop:5}}>Сумма в рублях</p>
            {sCalc&&<Calc value={aAmt} onChange={setAAmt} onClose={()=>setSCalc(false)}/>}
          </div>
          <div style={{marginBottom:18}}><p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Касса</p>
            <div style={{display:"flex",gap:8}}>{[{k:"cash",l:"Наличные",ic:IC.cash,c:"#22c55e",bg:"#f0fdf4"},{k:"card",l:"Безналичные",ic:IC.card,c:"#3b82f6",bg:"#eff6ff"}].map(w=>(<button key={w.k} className="wb" onClick={()=>setAWallet(w.k)} style={{flex:1,padding:"10px 14px",borderRadius:11,display:"flex",alignItems:"center",gap:8,border:aWallet===w.k?`2px solid ${w.c}`:"2px solid #e8eaed",background:aWallet===w.k?w.bg:"#fff",fontSize:12,fontWeight:aWallet===w.k?600:400,color:aWallet===w.k?w.c:"#667085",fontFamily:"inherit"}}><span style={{display:"flex"}}>{w.ic}</span>{w.l}</button>))}</div></div>
          <div style={{marginBottom:18}}><div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:8}}><p style={{fontSize:11,fontWeight:500,color:"#98a2b3",textTransform:"uppercase",letterSpacing:".5px"}}>Категория</p><button onClick={()=>setCatMgr(true)} style={{border:"none",background:"none",cursor:"pointer",color:"#98a2b3",display:"flex",padding:2,borderRadius:4,transition:"color .15s"}} onMouseEnter={e=>e.currentTarget.style.color="#3b82f6"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.gear}</button></div>
            <div style={{display:"flex",flexWrap:"wrap",gap:7}}>
              {(sAllCats?allCats:topCats).map(c=>(<button key={c} className="cc" onClick={()=>setACat(c)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,fontWeight:450,color:aCat===c?"#fff":"#344054",background:aCat===c?"#1a1d23":"#f0f2f5"}}>{c}</button>))}
              {!sAllCats&&allCats.length>4&&<button className="cc" onClick={()=>setSAllCats(true)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,fontWeight:450,color:"#3b82f6",background:"#eff6ff",display:"flex",alignItems:"center",gap:3}}>Ещё<span style={{display:"flex",transition:"transform .2s"}}>{IC.chevDown}</span></button>}
              {sAllCats&&(<>
                {/* Inline add - only in expanded */}
                {inlineAdd?(
                  <div style={{display:"flex",alignItems:"center",gap:4,animation:"fadeIn .15s"}}>
                    <input autoFocus value={inlineName} onChange={e=>setInlineName(e.target.value)} onKeyDown={e=>{if(e.key==="Enter")inlineAddCat();if(e.key==="Escape"){setInlineAdd(false);setInlineName("");}}} onBlur={()=>{if(!inlineName.trim()){setInlineAdd(false);setInlineName("");}}} placeholder="Название..." style={{width:100,padding:"6px 10px",borderRadius:18,border:"1px solid #e8eaed",outline:"none",fontSize:12,fontFamily:"inherit",color:"#1a1d23",background:"#fff"}}/>
                    <button onClick={inlineAddCat} style={{width:26,height:26,borderRadius:"50%",border:"none",background:"#1a1d23",color:"#fff",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0}}>{IC.check}</button>
                  </div>
                ):(
                  <button className="cc" onClick={()=>setInlineAdd(true)} style={{padding:"7px 12px",borderRadius:18,fontSize:12,fontWeight:450,color:"#667085",background:"#f0f2f5",display:"flex",alignItems:"center",gap:3}}>{IC.plusSm}</button>
                )}
                <button className="cc" onClick={()=>setSAllCats(false)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,fontWeight:450,color:"#3b82f6",background:"#eff6ff",display:"flex",alignItems:"center",gap:3}}>Свернуть<span style={{display:"flex",transform:"rotate(180deg)",transition:"transform .2s"}}>{IC.chevDown}</span></button>
              </>)}
            </div></div>
          <div style={{marginBottom:18}}><p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Описание</p><textarea value={aDesc} onChange={e=>setADesc(e.target.value)} placeholder="Комментарий..." rows={2} style={{width:"100%",padding:"10px 12px",borderRadius:11,border:"1px solid #e8eaed",background:"#f8f9fb",fontSize:13,fontFamily:"inherit",resize:"vertical",outline:"none",color:"#1a1d23",transition:"border .2s",minHeight:50,lineHeight:1.5}} onFocus={e=>e.currentTarget.style.borderColor="#3b82f6"} onBlur={e=>e.currentTarget.style.borderColor="#e8eaed"}/></div>
          <div style={{marginBottom:24,position:"relative"}}><p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Дата</p><button onClick={()=>setSCal(!sCal)} style={{display:"flex",alignItems:"center",gap:8,padding:"9px 14px",borderRadius:11,background:"#f8f9fb",border:sCal?"1px solid #3b82f6":"1px solid #e8eaed",fontSize:13,color:"#344054",cursor:"pointer",fontFamily:"inherit",width:"100%",transition:"border .2s"}}><span style={{color:"#3b82f6",display:"flex"}}>{IC.cal}</span>{fmtD(aDate)}{fmtD(aDate)===todayStr&&<span style={{fontSize:11,color:"#98a2b3"}}>(сегодня)</span>}</button>{sCal&&<CalPick sel={aDate} onSel={setADate} onClose={()=>setSCal(false)}/>}</div>
          <button className="sb" onClick={add} style={{width:"100%",padding:"13px",borderRadius:11,background:aType==="expense"?"linear-gradient(135deg,#ef4444,#dc2626)":"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",fontSize:14,fontWeight:600}}>Добавить {aType==="expense"?"расход":"доход"}</button>
        </div>
      </div>)}
      {catMgr&&<CatManager cats={cats} onUpdate={setCats} onClose={()=>setCatMgr(false)}/>}
    </div>
  );
}
