package com.pclpro.mobile;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.view.View;

/** Minimal vector icons drawn with Canvas so no drawable assets are needed. */
public class IconView extends View {

    public static final int GRID = 1;
    public static final int HOUSE = 2;
    public static final int GEAR = 3;
    public static final int CUBE = 4;
    public static final int SEARCH = 5;
    public static final int COPY = 6;
    public static final int PLAY = 7;
    public static final int REFRESH = 8;
    public static final int PLUS = 9;
    public static final int WIFI = 10;
    public static final int INFO = 11;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private final RectF rect = new RectF();
    private int type;
    private int color = Ui.BLUE;

    public IconView(Context c) {
        super(c);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setStrokeJoin(Paint.Join.ROUND);
    }

    public IconView type(int t) {
        this.type = t;
        invalidate();
        return this;
    }

    public IconView tint(int c) {
        this.color = c;
        invalidate();
        return this;
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        float w = getWidth();
        float h = getHeight();
        float s = Math.min(w, h);
        float cx = w / 2f;
        float cy = h / 2f;
        paint.setColor(color);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(s * 0.085f);
        paint.setAlpha(255);

        switch (type) {
            case GRID: {
                float cell = s * 0.30f;
                float gap = s * 0.08f;
                float[] xs = {cx - cell - gap / 2, cx + gap / 2};
                float[] ys = {cy - cell - gap / 2, cy + gap / 2};
                paint.setStyle(Paint.Style.FILL);
                for (float x : xs) {
                    for (float y : ys) {
                        rect.set(x, y, x + cell, y + cell);
                        canvas.drawRoundRect(rect, s * 0.07f, s * 0.07f, paint);
                    }
                }
                break;
            }
            case HOUSE: {
                paint.setStyle(Paint.Style.STROKE);
                path.reset();
                float roof = s * 0.30f;
                path.moveTo(cx - roof, cy - s * 0.02f);
                path.lineTo(cx, cy - s * 0.30f);
                path.lineTo(cx + roof, cy - s * 0.02f);
                canvas.drawPath(path, paint);
                rect.set(cx - s * 0.22f, cy - s * 0.02f, cx + s * 0.22f, cy + s * 0.30f);
                canvas.drawRoundRect(rect, s * 0.05f, s * 0.05f, paint);
                break;
            }
            case GEAR: {
                paint.setStyle(Paint.Style.STROKE);
                canvas.drawCircle(cx, cy, s * 0.16f, paint);
                paint.setStrokeWidth(s * 0.10f);
                for (int i = 0; i < 8; i++) {
                    double a = Math.PI / 4 * i;
                    float x1 = (float) (cx + Math.cos(a) * s * 0.24f);
                    float y1 = (float) (cy + Math.sin(a) * s * 0.24f);
                    float x2 = (float) (cx + Math.cos(a) * s * 0.32f);
                    float y2 = (float) (cy + Math.sin(a) * s * 0.32f);
                    canvas.drawLine(x1, y1, x2, y2, paint);
                }
                break;
            }
            case CUBE: {
                paint.setStyle(Paint.Style.FILL);
                path.reset();
                path.moveTo(cx, cy - s * 0.32f);
                path.lineTo(cx + s * 0.30f, cy - s * 0.15f);
                path.lineTo(cx, cy + s * 0.02f);
                path.lineTo(cx - s * 0.30f, cy - s * 0.15f);
                path.close();
                paint.setAlpha(245);
                canvas.drawPath(path, paint);

                path.reset();
                path.moveTo(cx - s * 0.30f, cy - s * 0.15f);
                path.lineTo(cx, cy + s * 0.02f);
                path.lineTo(cx, cy + s * 0.32f);
                path.lineTo(cx - s * 0.30f, cy + s * 0.15f);
                path.close();
                paint.setAlpha(170);
                canvas.drawPath(path, paint);

                path.reset();
                path.moveTo(cx + s * 0.30f, cy - s * 0.15f);
                path.lineTo(cx, cy + s * 0.02f);
                path.lineTo(cx, cy + s * 0.32f);
                path.lineTo(cx + s * 0.30f, cy + s * 0.15f);
                path.close();
                paint.setAlpha(120);
                canvas.drawPath(path, paint);
                paint.setAlpha(255);
                break;
            }
            case SEARCH: {
                paint.setStyle(Paint.Style.STROKE);
                canvas.drawCircle(cx - s * 0.05f, cy - s * 0.05f, s * 0.20f, paint);
                canvas.drawLine(cx + s * 0.10f, cy + s * 0.10f, cx + s * 0.28f, cy + s * 0.28f, paint);
                break;
            }
            case COPY: {
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(s * 0.075f);
                rect.set(cx - s * 0.22f, cy - s * 0.26f, cx + s * 0.12f, cy + s * 0.10f);
                canvas.drawRoundRect(rect, s * 0.05f, s * 0.05f, paint);
                rect.set(cx - s * 0.10f, cy - s * 0.12f, cx + s * 0.24f, cy + s * 0.26f);
                canvas.drawRoundRect(rect, s * 0.05f, s * 0.05f, paint);
                break;
            }
            case PLAY: {
                paint.setStyle(Paint.Style.FILL);
                path.reset();
                path.moveTo(cx - s * 0.16f, cy - s * 0.24f);
                path.lineTo(cx + s * 0.26f, cy);
                path.lineTo(cx - s * 0.16f, cy + s * 0.24f);
                path.close();
                canvas.drawPath(path, paint);
                break;
            }
            case REFRESH: {
                paint.setStyle(Paint.Style.STROKE);
                rect.set(cx - s * 0.26f, cy - s * 0.26f, cx + s * 0.26f, cy + s * 0.26f);
                canvas.drawArc(rect, -60, 280, false, paint);
                path.reset();
                path.moveTo(cx + s * 0.06f, cy - s * 0.34f);
                path.lineTo(cx + s * 0.28f, cy - s * 0.26f);
                path.lineTo(cx + s * 0.18f, cy - s * 0.04f);
                canvas.drawPath(path, paint);
                break;
            }
            case PLUS: {
                canvas.drawLine(cx - s * 0.24f, cy, cx + s * 0.24f, cy, paint);
                canvas.drawLine(cx, cy - s * 0.24f, cx, cy + s * 0.24f, paint);
                break;
            }
            case WIFI: {
                paint.setStyle(Paint.Style.STROKE);
                for (int i = 0; i < 3; i++) {
                    float r = s * (0.12f + i * 0.09f);
                    rect.set(cx - r, cy - r, cx + r, cy + r);
                    canvas.drawArc(rect, -135, 90, false, paint);
                }
                paint.setStyle(Paint.Style.FILL);
                canvas.drawCircle(cx, cy + s * 0.26f, s * 0.055f, paint);
                break;
            }
            case INFO:
            default: {
                paint.setStyle(Paint.Style.STROKE);
                canvas.drawCircle(cx, cy, s * 0.29f, paint);
                paint.setStyle(Paint.Style.FILL);
                canvas.drawCircle(cx, cy - s * 0.15f, s * 0.045f, paint);
                paint.setStrokeWidth(s * 0.09f);
                canvas.drawLine(cx, cy - s * 0.04f, cx, cy + s * 0.18f, paint);
                break;
            }
        }
    }
}
