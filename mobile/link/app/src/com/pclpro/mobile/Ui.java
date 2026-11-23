package com.pclpro.mobile;

import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Small UI toolkit so the whole app renders without any third-party dependency. */
public final class Ui {

    public static final int BLUE = 0xFF0E63C8;
    public static final int BLUE_DARK = 0xFF0B57A4;
    public static final int BLUE_SOFT = 0xFFE8F1FE;
    public static final int BG = 0xFFEEF3FA;
    public static final int CARD = 0xFFFFFFFF;
    public static final int TEXT = 0xFF1F2329;
    public static final int SUB = 0xFF7A8494;
    public static final int LINE = 0xFFE3EAF4;
    public static final int GREEN = 0xFF2FA36B;
    public static final int ORANGE = 0xFFE1912C;
    public static final int RED = 0xFFD9534F;
    public static final int WHITE = 0xFFFFFFFF;

    private Ui() {
    }

    public static int dp(Context c, float v) {
        return Math.round(TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, c.getResources().getDisplayMetrics()));
    }

    public static int spToPx(Context c, float sp) {
        return Math.round(TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_SP, sp, c.getResources().getDisplayMetrics()));
    }

    public static GradientDrawable round(int color, float radiusDp, Context c) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(color);
        d.setCornerRadius(dp(c, radiusDp));
        return d;
    }

    public static GradientDrawable stroked(int fill, float radiusDp, int strokeColor, float strokeDp, Context c) {
        GradientDrawable d = round(fill, radiusDp, c);
        d.setStroke(Math.max(1, dp(c, strokeDp)), strokeColor);
        return d;
    }

    /** Creates a pressed-state aware rounded background. */
    public static RippleDrawable pressable(int fill, float radiusDp, Context c) {
        return new RippleDrawable(ColorStateList.valueOf(0x220E63C8), round(fill, radiusDp, c), null);
    }

    public static TextView tv(Context c, CharSequence s, float sp, int color, boolean medium) {
        TextView t = new TextView(c);
        t.setText(s);
        t.setTextSize(sp);
        t.setTextColor(color);
        t.setLineSpacing(dp(c, 2), 1.0f);
        if (medium) {
            t.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        }
        return t;
    }

    public static LinearLayout row(Context c) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.HORIZONTAL);
        l.setGravity(Gravity.CENTER_VERTICAL);
        return l;
    }

    public static LinearLayout column(Context c) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.VERTICAL);
        return l;
    }

    public static LinearLayout.LayoutParams lp(int w, int h) {
        return new LinearLayout.LayoutParams(w, h);
    }

    public static LinearLayout.LayoutParams lpw(float weight) {
        return new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, weight);
    }

    public static void margins(View v, int l, int t, int r, int b) {
        ViewGroup.LayoutParams raw = v.getLayoutParams();
        if (raw instanceof ViewGroup.MarginLayoutParams) {
            ViewGroup.MarginLayoutParams p = (ViewGroup.MarginLayoutParams) raw;
            p.setMargins(dp(v.getContext(), l), dp(v.getContext(), t), dp(v.getContext(), r), dp(v.getContext(), b));
            v.setLayoutParams(p);
        }
    }

    /** PCL-style white card with 12dp radius, 1.5dp elevation and 12dp side margins. */
    public static LinearLayout card(Context c) {
        LinearLayout l = column(c);
        l.setBackground(round(CARD, 12, c));
        l.setPadding(dp(c, 16), dp(c, 14), dp(c, 16), dp(c, 14));
        l.setElevation(dp(c, 1.5f));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        p.setMargins(dp(c, 12), dp(c, 5), dp(c, 12), dp(c, 6));
        l.setLayoutParams(p);
        return l;
    }

    public static TextView button(Context c, String label, boolean primary) {
        TextView b = tv(c, label, 14, primary ? WHITE : BLUE, true);
        b.setGravity(Gravity.CENTER);
        b.setPadding(dp(c, 14), 0, dp(c, 14), 0);
        b.setMinHeight(dp(c, 44));
        b.setBackground(primary
                ? pressable(BLUE, 9, c)
                : pressable(CARD, 9, c));
        if (!primary) {
            GradientDrawable normal = stroked(CARD, 9, 0xFFC9D6E6, 1, c);
            GradientDrawable pressed = stroked(BLUE_SOFT, 9, 0xFFC9D6E6, 1, c);
            android.graphics.drawable.StateListDrawable sl = new android.graphics.drawable.StateListDrawable();
            sl.addState(new int[]{android.R.attr.state_pressed}, pressed);
            sl.addState(new int[]{}, normal);
            b.setBackground(sl);
        }
        b.setClickable(true);
        b.setFocusable(true);
        return b;
    }

    public static TextView chip(Context c, String label, int bg, int fg) {
        TextView t = tv(c, label, 11, fg, true);
        t.setPadding(dp(c, 8), dp(c, 3), dp(c, 8), dp(c, 3));
        t.setBackground(round(bg, 20, c));
        return t;
    }

    public static View divider(Context c) {
        View v = new View(c);
        v.setBackgroundColor(LINE);
        v.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, Math.max(1, dp(c, 0.8f))));
        return v;
    }

    public static void gap(Context c, LinearLayout parent, int heightDp) {
        View v = new View(c);
        v.setLayoutParams(new LinearLayout.LayoutParams(1, dp(c, heightDp)));
        parent.addView(v);
    }

    public static android.widget.EditText field(Context c, String hint) {
        android.widget.EditText e = new android.widget.EditText(c);
        e.setHint(hint);
        e.setTextSize(14);
        e.setTextColor(TEXT);
        e.setHintTextColor(0xFF9AA6B6);
        e.setSingleLine(true);
        e.setPadding(dp(c, 12), dp(c, 10), dp(c, 12), dp(c, 10));
        e.setBackground(stroked(0xFFF7FAFF, 8, LINE, 1, c));
        return e;
    }
}
